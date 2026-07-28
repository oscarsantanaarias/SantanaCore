using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SantanaLib.Collections.Concurrent;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Chat;
namespace Santana
{
  internal class Mailbox : IEnumerable<Mail>
  {
    private const string GiftMarker = "[NNGIFT:";
    public const int ItemsPerPage = 8;
    private readonly ConcurrentDictionary<ulong, Mail> _box = new ConcurrentDictionary<ulong, Mail>();
    private readonly ConcurrentStack<Mail> _trash = new ConcurrentStack<Mail>();
    public Mailbox(Player player, PlayerDto dto)
    {
      Player = player;
      foreach (var row in dto.Inbox)
      {
        if (row.IsMailDeleted)
          continue;
        _box.TryAdd((ulong)row.Id, new Mail(row, row.IsClubMail));
      }
    }
    public Player Player { get; }
    public int Count => _box.Count;
    public Mail this[ulong id] => CollectionExtensions.GetValueOrDefault(_box, id);
    public IEnumerator<Mail> GetEnumerator()
    {
      return _box.Values.GetEnumerator();
    }
    IEnumerator IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
    public IEnumerable<Mail> GetMailsByPage(byte page)
    {
      var startAt = ItemsPerPage * (page - 1);
      return _box.Values
          .OrderBy(entry => entry.SendDate.ToUnixTimeSeconds())
          .ThenBy(entry => entry.Id)
          .Skip(startAt)
          .Take(ItemsPerPage);
    }
    public IEnumerable<Mail> GetMailsByPage(byte page, Func<Mail, bool> predicate)
    {
      predicate ??= (_ => true);
      var startAt = ItemsPerPage * (page - 1);
      return _box.Values
          .Where(predicate)
          .OrderBy(entry => entry.SendDate.ToUnixTimeSeconds())
          .ThenBy(entry => entry.Id)
          .Skip(startAt)
          .Take(ItemsPerPage);
    }
    internal void Add(Mail mail)
    {
      if (_box.TryAdd(mail.Id, mail))
        UpdateReminder();
    }
    public async Task<bool> SendAsync(string receiver, string title, string message, bool isClub = false)
    {
      return await SendAsync(receiver, title, message, isClub, null, true);
    }
    public async Task<bool> SendGiftAsync(string receiver, string title, string message, NoteGiftDto gift)
    {
      return await SendAsync(receiver, title, message, false, gift, false);
    }
    public async Task<bool> SendRequestAsync(string receiver, string title, string message, NoteGiftDto gift)
    {
      return await SendSpecialAsync(receiver, title, Mail.EncodeRequestMessage(message, gift, 6), false);
    }
    public async Task<bool> SendTypedAsync(string receiver, string title, string message, int messageType)
    {
      var markerGift = new NoteGiftDto();
      return await SendSpecialAsync(receiver, title, Mail.EncodeRequestMessage(message, markerGift, messageType), false);
    }
    private async Task<bool> SendAsync(string receiver, string title, string message, bool isClub, NoteGiftDto gift,
      bool openedGift)
    {
      AccountDto target;
      using (var authDb = AuthDatabase.Open())
      {
        var matches = await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
            .Where($"{nameof(AccountDto.Nickname):C} = @{nameof(receiver)}")
            .WithParameters(new { receiver }));
        target = matches.FirstOrDefault();
      }
      if (target == null)
        return false;
      using (var gameDb = GameDatabase.Open())
      {
        var row = new PlayerMailDto
        {
          PlayerId = target.Id,
          SenderPlayerId = (int)Player.Account.Id,
          SentDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
          Title = title,
          Message = Mail.EncodeMessage(message, gift, openedGift),
          IsMailNew = true,
          IsMailDeleted = false,
          IsClubMail = isClub
        };
        await DbUtil.InsertAsync(gameDb, row);
        var online = GameServer.Instance.PlayerManager.Get(receiver);
        online?.Mailbox.Add(new Mail(row, isClub));
        online?.Mailbox.UpdateReminder();
        return true;
      }
    }
    private async Task<bool> SendSpecialAsync(string receiver, string title, string encodedMessage, bool isClub)
    {
      AccountDto target;
      using (var authDb = AuthDatabase.Open())
      {
        var matches = await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
            .Where($"{nameof(AccountDto.Nickname):C} = @{nameof(receiver)}")
            .WithParameters(new { receiver }));
        target = matches.FirstOrDefault();
      }
      if (target == null)
        return false;
      using (var gameDb = GameDatabase.Open())
      {
        var row = new PlayerMailDto
        {
          PlayerId = target.Id,
          SenderPlayerId = (int)Player.Account.Id,
          SentDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
          Title = title,
          Message = encodedMessage,
          IsMailNew = true,
          IsMailDeleted = false,
          IsClubMail = isClub
        };
        await DbUtil.InsertAsync(gameDb, row);
        var online = GameServer.Instance.PlayerManager.Get(receiver);
        online?.Mailbox.Add(new Mail(row, isClub));
        online?.Mailbox.UpdateReminder();
        return true;
      }
    }
    public bool Remove(IEnumerable<Mail> mails)
    {
      return Remove(mails.Select(entry => entry.Id));
    }
    public bool Remove(IEnumerable<ulong> mailIds)
    {
      var removedAny = false;
      foreach (var id in mailIds)
      {
        var entry = this[id];
        if (entry == null)
          continue;
        removedAny = true;
        _box.Remove(entry.Id);
        _trash.Push(entry);
      }
      UpdateReminder();
      return removedAny;
    }
    public void UpdateReminder()
    {
      var unread = (byte)this.Count(m => m.IsNew);
      Player.ChatSession.SendAsync(new NoteCountAckMessage(unread, 0, 0));
    }
    internal void Save(IDbConnection db)
    {
      var deletedMapping = OrmConfiguration
          .GetDefaultEntityMapping<PlayerMailDto>()
          .Clone()
          .UpdatePropertiesExcluding(prop => prop.IsExcludedFromUpdates = true,
              nameof(PlayerMailDto.IsMailDeleted));
      if (!_trash.IsEmpty)
      {
        var deletedIds = new StringBuilder();
        var first = true;
        while (_trash.TryPop(out var gone))
        {
          if (first)
            first = false;
          else
            deletedIds.Append(',');
          deletedIds.Append(gone.Id);
        }
        DbUtil.BulkUpdate(db, new PlayerMailDto { IsMailDeleted = true }, statement => statement
              .Where($"{nameof(PlayerMailDto.Id):C} IN ({deletedIds})")
              .WithEntityMappingOverride(deletedMapping));
      }
      var newFlagMapping = OrmConfiguration
          .GetDefaultEntityMapping<PlayerMailDto>()
          .Clone()
          .UpdatePropertiesExcluding(prop => prop.IsExcludedFromUpdates = true,
              nameof(PlayerMailDto.IsMailNew));
      var bodyMapping = OrmConfiguration
          .GetDefaultEntityMapping<PlayerMailDto>()
          .Clone()
          .UpdatePropertiesExcluding(prop => prop.IsExcludedFromUpdates = true,
              nameof(PlayerMailDto.Message));
      var dirty = _box.Values.Where(entry => entry.NeedsToSave).ToArray();
      var bodyChanged = dirty.Where(entry => entry.MessageNeedsSave).ToArray();
      var becameNew = dirty.Where(entry => entry.IsNew);
      var becameRead = dirty.Where(entry => !entry.IsNew);
      if (bodyChanged.Any())
      {
        foreach (var entry in bodyChanged)
        {
          DbUtil.Update(db,
              new PlayerMailDto { Id = (int)entry.Id, Message = entry.StoredMessage },
              statement => statement.WithEntityMappingOverride(bodyMapping));
          entry.MessageNeedsSave = false;
        }
      }
      var newOnes = becameNew as Mail[] ?? becameNew.ToArray();
      if (newOnes.Any())
      {
        DbUtil.BulkUpdate(db, new PlayerMailDto { IsMailNew = true }, statement => statement
              .Where($"{nameof(PlayerMailDto.Id):C} IN ({string.Join(",", newOnes.Select(x => x.Id))})")
              .WithEntityMappingOverride(newFlagMapping));
        foreach (var entry in newOnes)
          entry.NeedsToSave = false;
      }
      var readOnes = becameRead as Mail[] ?? becameRead.ToArray();
      if (readOnes.Any())
      {
        DbUtil.BulkUpdate(db, new PlayerMailDto { IsMailNew = false }, statement => statement
              .Where($"{nameof(PlayerMailDto.Id):C} IN ({string.Join(",", readOnes.Select(x => x.Id))})")
              .WithEntityMappingOverride(newFlagMapping));
        foreach (var entry in readOnes)
          entry.NeedsToSave = false;
      }
    }
  }
  internal class Mail
  {
    private const string GiftPrefix = "[NNGIFT:";
    private const string RequestPrefix = "[NNREQ:";
    private bool _isNew;
    private string _storedMessage;
    internal Mail(PlayerMailDto mailDto, bool isClan = false)
    {
      Id = (ulong)mailDto.Id;
      ReceiverId = (ulong)mailDto.PlayerId;
      SenderId = (ulong)mailDto.SenderPlayerId;
      Sender = GetNickname(SenderId);
      SendDate = DateTimeOffset.FromUnixTimeSeconds(mailDto.SentDate);
      Title = mailDto.Title;
      RawMessage = mailDto.Message ?? "";
      Message = RawMessage;
      _storedMessage = RawMessage;
      _isNew = mailDto.IsMailNew;
      IsClan = isClan;
      MessageType = isClan ? 1 : 0;
      if (TryParseRequestMessage(RawMessage, out var requestBody, out var requestGift, out var requestType))
      {
        Message = requestBody;
        Gift = requestGift;
        MessageType = requestType;
        OpenedGift = true;
      }
      else if (TryParseGiftMessage(RawMessage, out var body, out var gift, out var openedGift))
      {
        Message = body;
        Gift = gift;
        MessageType = openedGift ? 8 : 5;
        OpenedGift = openedGift;
      }
      else
      {
        Gift = new NoteGiftDto();
        OpenedGift = true;
      }
    }
    internal bool NeedsToSave { get; set; }
    internal bool MessageNeedsSave { get; set; }
    public ulong Id { get; }
    public string Sender { get; }
    public ulong SenderId { get; }
    public ulong ReceiverId { get; }
    public DateTimeOffset SendDate { get; }
    public DateTimeOffset Expires => SendDate.AddDays(30);
    public string Title { get; }
    public string RawMessage { get; }
    public string Message { get; private set; }
    public string StoredMessage => _storedMessage;
    public bool IsClan { get; }
    public int MessageType { get; private set; }
    public bool OpenedGift { get; private set; }
    public NoteGiftDto Gift { get; }
    public bool IsGift => MessageType == 5 || MessageType == 8;
    public bool IsRequest => MessageType == 6;
    public bool IsResolvedRequest => MessageType == 7;
    public bool IsNew
    {
      get => _isNew;
      internal set
      {
        if (_isNew == value)
          return;
        _isNew = value;
        NeedsToSave = true;
      }
    }
    internal void MarkGiftOpened()
    {
      if (!IsGift || OpenedGift)
        return;
      OpenedGift = true;
      MessageType = 8;
      _storedMessage = EncodeMessage(Message, Gift, true);
      MessageNeedsSave = true;
      NeedsToSave = true;
    }
    internal void MarkRequestResolved(string resolutionText = null)
    {
      if (!IsRequest || MessageType == 7)
        return;
      if (!string.IsNullOrWhiteSpace(resolutionText))
        Message = resolutionText;
      MessageType = 7;
      _storedMessage = EncodeRequestMessage(Message, Gift, 7);
      MessageNeedsSave = true;
      NeedsToSave = true;
    }
    private static string GetNickname(ulong id)
    {
      var online = GameServer.Instance.PlayerManager[id];
      if (online != null)
        return online.Account.Nickname;
      using (var authDb = AuthDatabase.Open())
      {
        return DbUtil.Get(authDb, new AccountDto { Id = (int)id })?.Nickname ?? "";
      }
    }
    internal static string EncodeMessage(string message, NoteGiftDto gift, bool openedGift)
    {
      if (gift == null)
        return message ?? "";
      return $"{GiftPrefix}{gift.Unk1};{gift.Unk2};{(uint)gift.ItemNumber};{(int)gift.PriceType};{(int)gift.PeriodType};{gift.Period};{gift.Unk5};{gift.Unk6};{gift.Unk7};{(openedGift ? 1 : 0)}]{message ?? ""}";
    }
    internal static string EncodeRequestMessage(string message, NoteGiftDto gift, int messageType)
    {
      if (gift == null)
        return message ?? "";
      return $"{RequestPrefix}{messageType};{gift.Unk1};{gift.Unk2};{(uint)gift.ItemNumber};{(int)gift.PriceType};{(int)gift.PeriodType};{gift.Period};{gift.Unk5};{gift.Unk6};{gift.Unk7}]{message ?? ""}";
    }
    private static bool TryParseGiftMessage(string rawMessage, out string body, out NoteGiftDto gift, out bool openedGift)
    {
      body = rawMessage ?? "";
      gift = null;
      openedGift = true;
      if (string.IsNullOrEmpty(rawMessage) || !rawMessage.StartsWith(GiftPrefix, StringComparison.Ordinal))
        return false;
      var end = rawMessage.IndexOf(']');
      if (end <= GiftPrefix.Length)
        return false;
      var header = rawMessage.Substring(GiftPrefix.Length, end - GiftPrefix.Length);
      var parts = header.Split(';');
      if (parts.Length < 8)
        return false;
      uint unk1 = 0;
      uint unk2 = 0;
      var baseIndex = 0;
      if (parts.Length >= 10 &&
          uint.TryParse(parts[0], out unk1) &&
          uint.TryParse(parts[1], out unk2))
        baseIndex = 2;
      if (!uint.TryParse(parts[baseIndex + 0], out var itemNumber) ||
          !int.TryParse(parts[baseIndex + 1], out var priceType) ||
          !int.TryParse(parts[baseIndex + 2], out var periodType) ||
          !ushort.TryParse(parts[baseIndex + 3], out var period) ||
          !byte.TryParse(parts[baseIndex + 4], out var unk5) ||
          !int.TryParse(parts[baseIndex + 5], out var unk6) ||
          !byte.TryParse(parts[baseIndex + 6], out var unk7) ||
          !int.TryParse(parts[baseIndex + 7], out var opened))
        return false;
      body = rawMessage.Substring(end + 1);
      gift = new NoteGiftDto
      {
        Unk1 = unk1,
        Unk2 = unk2,
        ItemNumber = itemNumber,
        PriceType = (ItemPriceType)priceType,
        PeriodType = (ItemPeriodType)periodType,
        Period = period,
        Unk5 = unk5,
        Unk6 = unk6,
        Unk7 = unk7
      };
      openedGift = opened != 0;
      return true;
    }
    private static bool TryParseRequestMessage(string rawMessage, out string body, out NoteGiftDto gift, out int messageType)
    {
      body = rawMessage ?? "";
      gift = null;
      messageType = 6;
      if (string.IsNullOrEmpty(rawMessage) || !rawMessage.StartsWith(RequestPrefix, StringComparison.Ordinal))
        return false;
      var end = rawMessage.IndexOf(']');
      if (end <= RequestPrefix.Length)
        return false;
      var header = rawMessage.Substring(RequestPrefix.Length, end - RequestPrefix.Length);
      var parts = header.Split(';');
      if (parts.Length < 8)
        return false;
      uint unk1 = 0;
      uint unk2 = 0;
      var baseIndex = 1;
      if (!int.TryParse(parts[0], out messageType))
        return false;
      if (parts.Length >= 10 &&
          uint.TryParse(parts[1], out unk1) &&
          uint.TryParse(parts[2], out unk2))
        baseIndex = 3;
      if (!uint.TryParse(parts[baseIndex + 0], out var itemNumber) ||
          !int.TryParse(parts[baseIndex + 1], out var priceType) ||
          !int.TryParse(parts[baseIndex + 2], out var periodType) ||
          !ushort.TryParse(parts[baseIndex + 3], out var period) ||
          !byte.TryParse(parts[baseIndex + 4], out var unk5) ||
          !int.TryParse(parts[baseIndex + 5], out var unk6) ||
          !byte.TryParse(parts[baseIndex + 6], out var unk7))
        return false;
      body = rawMessage.Substring(end + 1);
      gift = new NoteGiftDto
      {
        Unk1 = unk1,
        Unk2 = unk2,
        ItemNumber = itemNumber,
        PriceType = (ItemPriceType)priceType,
        PeriodType = (ItemPeriodType)periodType,
        Period = period,
        Unk5 = unk5,
        Unk6 = unk6,
        Unk7 = unk7
      };
      return true;
    }
  }
}

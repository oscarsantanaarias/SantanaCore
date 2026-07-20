using System;
using System.Linq;
using System.Threading.Tasks;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using ExpressMapper.Extensions;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Chat;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;

namespace Santana.Network.Services
{
  internal class MSGService : ProudMessageHandler
  {
    private static readonly ILogger Log_ =
        Serilog.Log.ForContext(Constants.SourceContextPropertyName, nameof(MSGService));

    [MessageHandler(typeof(NoteListReqMessage))]
    public void CNoteListReq(ChatSession session, NoteListReqMessage message)
    {
      Log_.ForAccount(session)
          .Debug("Mailbox page {page} requested, filter {messageType}", message.Page, message.MessageType);

      var wantedType = message.MessageType;
      Func<Mail, bool> keep = entry =>
      {
        if (wantedType >= 0 && wantedType <= 8 && wantedType != 3)
          return entry.MessageType == wantedType;
        return true;
      };

      var inbox = session.Player.Mailbox;
      var visibleCount = inbox.Count(keep);
      var pageCount = Math.Max(1, (visibleCount + Mailbox.ItemsPerPage - 1) / Mailbox.ItemsPerPage);

      if (message.Page > pageCount)
      {
        Log_.ForAccount(session)
            .Error("Mailbox has no page {page}, request ignored", message.Page);
        return;
      }

      var pageRows = inbox.GetMailsByPage(message.Page, keep);
      foreach (var row in pageRows)
      {
        Log_.ForAccount(session)
            .Debug(
                "Listing mail {id}: type {type}, unread {isNew}, giftOpened {openedGift}, gift {isGift}, request {isRequest}, requestDone {isResolvedRequest}, subject {title}",
                row.Id,
                row.MessageType,
                row.IsNew,
                row.OpenedGift,
                row.IsGift,
                row.IsRequest,
                row.IsResolvedRequest,
                row.Title);
      }

      session.SendAsync(new NoteListAckMessage(pageCount, message.Page,
          pageRows.Select(row => row.Map<Mail, NoteDto>()).ToArray()));
    }

    [MessageHandler(typeof(NoteReadReqMessage))]
    public void CReadNoteReq(ChatSession session, NoteReadReqMessage message)
    {
      Log_.ForAccount(session)
          .Debug("Opening mail {id}", message.Id);

      var entry = session.Player.Mailbox[message.Id];
      if (entry == null)
      {
        Log_.ForAccount(session)
            .Error("Open failed: mail {id} is not in this mailbox", message.Id);

        session.SendAsync(new NoteReadAckMessage(0, new NoteContentDto(), 0));
        return;
      }

      Log_.ForAccount(session)
          .Debug(
              "Mail {id} before marking read: type {type}, unread {isNew}, giftOpened {openedGift}, gift {isGift}, request {isRequest}, requestDone {isResolvedRequest}, subject {title}",
              entry.Id,
              entry.MessageType,
              entry.IsNew,
              entry.OpenedGift,
              entry.IsGift,
              entry.IsRequest,
              entry.IsResolvedRequest,
              entry.Title);

      if (entry.IsRequest)
        session.LastReadRequestMailId = entry.Id;

      Log_.ForAccount(session)
          .Debug(
              "Mail {id} contents: from {sender}, toId {receiverId}, item {item}, priceType {priceType}, periodType {periodType}, period {period}, color {color}, flags {flags}, mode {mode}, text {body}",
              entry.Id,
              entry.Sender ?? "",
              entry.ReceiverId,
              (uint)entry.Gift.ItemNumber,
              (int)entry.Gift.PriceType,
              (int)entry.Gift.PeriodType,
              entry.Gift.Period,
              entry.Gift.Color,
              entry.Gift.Flags,
              entry.Gift.Mode,
              entry.Message ?? "");

      entry.IsNew = false;
      session.Player.Mailbox.UpdateReminder();

      var payload = entry.Map<Mail, NoteContentDto>();
      Log_.ForAccount(session)
          .Debug(
              "Reply built for mail {id}: unk1 {unk1}, item slots {item0}/{item1}/{item2}/{item3}/{item4}/{item5}, unk2 {unk2}, unk3 {unk3}, text {body}",
              entry.Id,
              payload.Unk1,
              payload.Item.Unk0,
              payload.Item.Unk1,
              payload.Item.Unk2,
              payload.Item.Unk3,
              payload.Item.Unk4,
              payload.Item.Unk5,
              payload.Unk2,
              payload.Unk3,
              payload.Message ?? "");

      var popup = entry.IsGift || entry.IsRequest ? 2 : 1;
      session.SendAsync(new NoteReadAckMessage(entry.Id, payload, popup));
    }

    [MessageHandler(typeof(NoteDeleteReqMessage))]
    public void CDeleteNoteReq(ChatSession session, NoteDeleteReqMessage message)
    {
      Log_.ForAccount(session)
          .Debug("Removing mail ids {id}", string.Join(",", message.Notes));

      session.Player.Mailbox.Remove(message.Notes);
      session.SendAsync(new NoteDeleteAckMessage());
    }

    [MessageHandler(typeof(NoteSendReqMessage))]
    public async Task CSendNoteReq(ChatSession session, NoteSendReqMessage message)
    {
      Log_.ForAccount(session)
          .Debug("Outgoing mail with subject {message}", message.Title);

      if (message.Title.Length > 100)
      {
        Log_.ForAccount(session)
            .Error("Send blocked: subject runs {length} chars, over the limit", message.Title.Length);
        return;
      }

      if (message.Message.Length > 112)
      {
        Log_.ForAccount(session)
            .Error("Send blocked: body runs {length} chars, over the limit", message.Message.Length);
        return;
      }

      var delivered = await session.Player.Mailbox.SendAsync(message.Receiver, message.Title, message.Message);
      await session.SendAsync(new NoteSendAckMessage(delivered ? 0 : 1));
    }

    [MessageHandler(typeof(NoteRejectImportuneReqMessage))]
    public async Task NoteRejectImportuneReq(ChatSession session, NoteRejectImportuneReqMessage message)
    {
      Log_.ForAccount(session)
          .Debug("Turning down a gift request, action {action}, id {id}", message.Unk1, message.Unk2);

      var wantedId = message.Unk2 != 0 ? (ulong)message.Unk2 : session.LastReadRequestMailId;
      var entry = wantedId != 0
          ? session.Player.Mailbox[wantedId]
          : session.Player.Mailbox
              .Where(x => x.IsRequest)
              .OrderByDescending(x => x.SendDate)
              .ThenByDescending(x => x.Id)
              .FirstOrDefault();

      Log_.ForAccount(session)
          .Debug(
              "Gift request refusal resolved: action {action}, id {id}, lastRead {fallbackId}, picked {targetId}, exists {found}, typeBefore {type}, willResolve {resolve}",
              message.Unk1,
              message.Unk2,
              session.LastReadRequestMailId,
              entry?.Id ?? wantedId,
              entry != null,
              entry?.MessageType ?? -1,
              entry != null && entry.IsRequest);

      if (entry != null && entry.IsRequest)
      {
        entry.MarkRequestResolved("Request refused.");
        session.LastReadRequestMailId = 0;

        await session.Player.Mailbox.SendAsync(
            entry.Sender,
            "Gift",
            $"{session.Player.Account.Nickname} has refused your gift request.");

        Log_.ForAccount(session)
            .Debug("Mail {id} now sits at type {type} after the refusal", entry.Id, entry.MessageType);
      }

      await session.SendAsync(new NoteRejectImportuneAckMessage
      {
        Unk = 0
      });
    }
  }
}

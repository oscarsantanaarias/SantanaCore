using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network.Data.Game;
using Santana.Network.Message.Game;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;
namespace Santana.Network.Services
{
    internal class CharService : ProudMessageHandler
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(CharService));
        [MessageHandler(typeof(CharacterFirstCreateReqMessage))]
        public async Task CharacterFirstCreateHandler(GameSession session, CharacterFirstCreateReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var badGender = !Enum.IsDefined(typeof(CharacterGender), (byte)message.Gender);
            var badItems = message.FirstItems == null || message.FirstItems.Length > 32;
            if (!Namecheck.IsNameValid(message.Nickname, true) || badItems || badGender)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
                return;
            }
            var owner = session.Player;
            var roster = owner.CharacterManager;
            if (roster.Any() && owner.Account.Nickname != string.Empty)
                return;
            try
            {
                roster.Remove(0);
                roster.Remove(1);
                roster.Remove(2);
            }
            catch
            {
            }
            if (string.IsNullOrWhiteSpace(owner.Account.Nickname))
            {
                if (!await AuthService.IsNickAvailableAsync(message.Nickname))
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.NicknameUnavailable));
                    return;
                }
                using (var authConn = AuthDatabase.Open())
                {
                    var accountRow = (await DbUtil.FindAsync<AccountDto>(authConn, smtp => smtp
                        .Where($"{nameof(AccountDto.Id):C} = @Id")
                        .WithParameters(new { session.Player.Account.Id })
                    )).FirstOrDefault();
                    if (accountRow == null)
                    {
                        await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
                        return;
                    }
                    accountRow.Nickname = message.Nickname;
                    await DbUtil.UpdateAsync(authConn, accountRow);
                    owner.Account.Nickname = message.Nickname;
                }
            }
            var grantedItems = new List<RequitalGiveItemResultDto>();
            try
            {
                roster.CreateFirst(0, (CharacterGender)message.Gender);
                roster.Select(0, true);
                try
                {
                    var take = 6;
                    if (message.FirstItems.Length < take)
                        take = message.FirstItems.Length;
                    for (var slot = 0; slot < take; slot++)
                    {
                        var costumeNumber = message.FirstItems[slot];
                        if (costumeNumber == 0)
                            continue;
                        grantedItems.Add(new RequitalGiveItemResultDto(costumeNumber, 0));
                        var freshItem = owner.Inventory.CreateSilent(costumeNumber, 0, 0, 1);
                        roster.CurrentCharacter.Costumes.Equip(freshItem, (CostumeSlot)freshItem.ItemNumber.SubCategory, true);
                    }
                }
                catch (ArgumentException e)
                {
                    Logger.Debug(e, "Starter loadout for the initial slot could not be materialized");
                }
            }
            catch (CharacterException ex)
            {
                Logger.ForAccount(session)
                    .Error(ex.Message);
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
                return;
            }
            await session.SendAsync(new RequitalGiveItemResultAckMessage(grantedItems.ToArray()));
            IEnumerable<StartItemDto> defaults;
            using (var gameConn = GameDatabase.Open())
            {
                defaults = await DbUtil.FindAsync<StartItemDto>(gameConn, statement => statement
                    .Where(
                        $"{nameof(StartItemDto.RequiredSecurityLevel):C} <= @{nameof(owner.Account.SecurityLevel)}")
                    .WithParameters(new { owner.Account.SecurityLevel }));
            }
            foreach (var entry in defaults)
            {
                var shopTable = GameServer.Instance.ResourceCache.GetShop();
                var group = shopTable.Items.Values.FirstOrDefault(g => g.GetItemInfo(entry.ShopItemInfoId) != null);
                if (group == null)
                {
                    continue;
                }
                var info = group.GetItemInfo(entry.ShopItemInfoId);
                var effectDef = info.EffectGroup.GetEffect(entry.ShopEffectId);
                var priceDef = info.PriceGroup.GetPrice(entry.ShopPriceId);
                if (priceDef == null)
                {
                    continue;
                }
                var tint = entry.Color;
                if (tint > group.ColorGroup)
                {
                    tint = 0;
                }
                var stack = entry.Count;
                if (stack > 0 && group.ItemNumber.Category <= ItemCategory.Skill)
                {
                    stack = 0;
                }
                if (stack < 0) stack = 0;
                var effectList = new List<EffectNumber> { effectDef.Effect };
                owner.Inventory.CreateSilent(info, priceDef, tint, effectList.ToArray(), (uint)stack);
            }
            owner.NeedsToSave = true;
            owner.Save();
            await AuthService.LoginAsync(session);
        }
        [MessageHandler(typeof(CharacterCreateReqMessage))]
        public async Task CreateCharacterHandler(GameSession session, CharacterCreateReqMessage message)
        {
            if (session?.Player == null || message == null || message.Style == null)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
                return;
            }
            var owner = session.Player;
            Logger.ForAccount(session)
                .Information("Allocating a fresh roster entry at index {slot}", message.Slot);
            try
            {
                owner.CharacterManager.Create(message.Slot, message.Style.Gender);
                owner.NeedsToSave = true;
                owner.Save();
            }
            catch (CharacterException ex)
            {
                Logger.ForAccount(session)
                    .Error(ex, "Roster allocation was rejected by the character manager");
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CreateCharacterFailed));
            }
        }
        [MessageHandler(typeof(CharacterSelectReqMessage))]
        public async Task SelectCharacterHandler(GameSession session, CharacterSelectReqMessage message)
        {
            var owner = session.Player;
            if (owner == null || message == null)
                return;
            if (owner.Room?.Id > 0 &&
                owner.Room?.GameState != GameState.Waiting &&
                owner.Room?.SubGameState != GameTimeState.HalfTime &&
                owner.RoomInfo.State != PlayerState.Lobby)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.SelectCharacterFailed));
                return;
            }
            Logger.ForAccount(session)
                .Information("Switching the active roster entry to index {slot}", message.Slot);
            try
            {
                if (owner.Room?.SubGameState == GameTimeState.HalfTime &&
                    !CustomRuleRooms.CustomRules(owner, message.Slot))
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.SelectCharacterFailed));
                    return;
                }
                if (owner.Room?.GameState == GameState.Playing &&
                    Room.CustomRules(owner, true) == false)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.SelectCharacterFailed));
                    return;
                }
                var previousNameTag = owner.NameTag;
                if (!owner.CharacterManager.Select(message.Slot))
                {
                    await session.SendAsync(new CharacterSelectAckMessage(message.Slot));
                    return;
                }
                using (var gameConn = GameDatabase.Open())
                {
                    var chip = DbUtil.Find<EsperSkillDto>(gameConn, statement => statement
                        .Where($"{nameof(EsperSkillDto.PlayerId):C} = @PlayerId AND {nameof(EsperSkillDto.CharId):C} = @CharId")
                        .WithParameters(new
                        {
                            PlayerId = owner.Account.Id,
                            CharId = message.Slot
                        }))
                        .FirstOrDefault();
                    if (chip == null)
                        await owner.SendAsync(new EspherChipLv5Message());
                    else
                        await owner.SendAsync(new EspherChipLv5Message(chip.Id));
                }
                AuthService.LoadPlayerNameTag(owner, true, false);
                if (owner.NameTag != previousNameTag)
                {
                    owner.KickToServerSelectionForNameTagChange(previousNameTag, owner.NameTag);
                }
            }
            catch (CharacterException ex)
            {
                Logger.ForAccount(session)
                    .Error(ex, "Active-entry switch was rejected by the character manager");
                await session.SendAsync(new ServerResultAckMessage(ServerResult.SelectCharacterFailed));
            }
            catch (Exception ex)
            {
                Logger.ForAccount(session)
                    .Error(ex, "Active-entry switch blew up outside the expected failure paths");
                await session.SendAsync(new ServerResultAckMessage(ServerResult.SelectCharacterFailed));
            }
        }
        [MessageHandler(typeof(CharacterDeleteReqMessage))]
        public void DeleteCharacterHandler(GameSession session, CharacterDeleteReqMessage message)
        {
            Logger.ForAccount(session)
                .Information("Discarding the roster entry held at index {slot}", message.Slot);
            var owner = session.Player;
            if (owner == null)
                return;
            if (owner.Room?.Id > 0 &&
                owner.Room?.GameState != GameState.Waiting &&
                owner.Room?.SubGameState != GameTimeState.HalfTime &&
                owner.RoomInfo.State != PlayerState.Lobby)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.DeleteCharacterFailed));
                return;
            }
            try
            {
                session.Player?.CharacterManager?.Remove(message.Slot);
            }
            catch (Exception ex)
            {
                Logger.ForAccount(session).Error(ex.Message);
                session.SendAsync(new ServerResultAckMessage(ServerResult.DeleteCharacterFailed));
            }
        }
    }
}

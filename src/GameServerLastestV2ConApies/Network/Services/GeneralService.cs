namespace Santana.Network.Services
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using SantanaLib.IO;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using Santana.Database.Auth;
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Game;
    using ProudNetSrc.Handlers;
    using Serilog;
    using Serilog.Core;
    using ProudNetSrc;
    using ProudNetSrc.Serialization;
    using System.Text;
    using Santana.Network.Message.Chat;
    using System.Collections.Generic;
    using Santana.Database.Game;
    using Newtonsoft.Json.Linq;
    using Santana.Network.Message.GameRule;
    using GameShopItemDto = Santana.Network.Data.Game.ShopItemDto;
    internal class GeneralService : ProudMessageHandler
    {
        public static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(GeneralService));
        public static int NoteGiftItemAckSuccessValue = 3;
        public static int NoteGiftItemAckFailValue = 0;
        public static int NoteGiftItemAckShortApValue = 1;
        public static int NoteGiftItemAckLevelLowValue = 4;
        public static int NoteImportuneItemAckSuccessValue = 3;
        public static int NoteImportuneItemAckFailValue = 0;
        public static async Task<bool> ChangeNickname(Player plr, NicknameHistoryDto nicknameHistory, bool restore)
        {
            var requestedNick = nicknameHistory.NewNickname;
            try
            {
                using (var conn = AuthDatabase.Open())
                {
                    var accountRow = (await DbUtil.FindAsync<AccountDto>(conn, statement => statement
                            .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                            .WithParameters(new { plr.Account.Nickname }))).FirstOrDefault();
                    if (accountRow == null)
                    {
                        await plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                        return false;
                    }
                    if (restore)
                    {
                        var historyRows = await DbUtil.FindAsync<NicknameHistoryDto>(conn, statement => statement
                            .Where($"{nameof(NicknameHistoryDto.AccountId):C} = @Id")
                            .WithParameters(new { plr.Account.Id }));
                        var earliest = historyRows.FirstOrDefault();
                        if (earliest == null)
                        {
                            await plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.NicknameUnavailable));
                            return false;
                        }
                        accountRow.Nickname = earliest.OldName;
                        plr.Account.Nickname = earliest.OldName;
                        foreach (var row in historyRows)
                            await DbUtil.DeleteAsync(conn, row);
                        await plr.Session.SendAsync(new ItemUseChangeNickCancelAckMessage(0));
                        DbUtil.Update(conn, accountRow);
                        return true;
                    }
                    if (!await AuthService.IsNickAvailableAsync(requestedNick))
                    {
                        await plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.NicknameUnavailable));
                        return false;
                    }
                    accountRow.Nickname = requestedNick;
                    plr.Account.Nickname = requestedNick;
                    DbUtil.Insert(conn, nicknameHistory);
                    await plr.Session.SendAsync(new ItemUseChangeNickAckMessage
                    {
                        Result = 0,
                        Unk2 = 0,
                        Unk3 = requestedNick
                    });
                    await plr.Session.SendAsync(new ItemUseChangeNickAckMessage
                    {
                        Result = 0,
                        Unk2 = 0L,
                        Unk3 = requestedNick
                    });
                    plr.Inventory.CreateSilent(4000002, 0, 0, 0);
                    DbUtil.Update(conn, accountRow);
                    return true;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
                await plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return false;
            }
        }
        [MessageHandler(typeof(TimeSyncReqMessage))]
        public void TimeSyncHandler(GameSession session, TimeSyncReqMessage message)
        {
            if (session.Player == null || message?.Time == null)
                return;
            if (message.Time == uint.MaxValue)
                throw new Exception("OutOfRange");
            session?.SendAsync(new TimeSyncAckMessage
            {
                ClientTime = message.Time,
                ServerTime = (uint)Environment.TickCount
            });
        }

        [MessageHandler(typeof(CheckHashKeyValueReqMessage))]
        public void CheckHashKeyValueReq(GameSession session, CheckHashKeyValueReqMessage message)
        {
        }
        [MessageHandler(typeof(NickCheckReqMessage))]
        public async Task CheckNickHandler(GameSession session, NickCheckReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            var asciiOnly = Config.Instance.Game.NickRestrictions.AsciiOnly;
            if (!await AuthService.IsNickAvailableAsync(message.Nickname))
                await actor.Session.SendAsync(new ServerResultAckMessage(ServerResult.NicknameUnavailable));
            if (Namecheck.IsNameValid(message.Nickname, true) && (!asciiOnly || !message.Nickname.Contains(" ") && (asciiOnly || Encoding.UTF8.GetByteCount(message.Nickname) != message.Nickname.Length)))
                await session.SendAsync(new NickCheckAckMessage(false));
            else
                await session.SendAsync(new NickCheckAckMessage(true));
        }
        [MessageHandler(typeof(ItemUseChangeNickCancelReqMessage))]
        public async Task ItemUseChangeNickCancelReq(GameSession session, ItemUseChangeNickCancelReqMessage message)
        {
            if (session.Player == null)
                return;
            var changeTicket = session.Player.Inventory.FirstOrDefault(x => x.ItemNumber == 4000002);
            if (changeTicket == null)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (await ChangeNickname(session.Player, new NicknameHistoryDto(), true))
                session.Player.Inventory.RemoveOrDecrease(changeTicket);
        }
        [MessageHandler(typeof(ItemUseChangeNickReqMessage))]
        public async Task UseChangeNameItem(GameSession session, ItemUseChangeNickReqMessage message)
        {
            var actor = session.Player;
            var ticket = actor.Inventory[message.ItemId];
            var asciiOnly = Config.Instance.Game.NickRestrictions.AsciiOnly;
            if (!await AuthService.IsNickAvailableAsync(message.Nickname))
                await actor.Session.SendAsync(new ServerResultAckMessage(ServerResult.NicknameUnavailable));
            if (!Namecheck.IsNameValid(message.Nickname, true) || asciiOnly && message.Nickname.Any(c => c > 127) ||
                !asciiOnly && message.Nickname.Any(c => c > 255))
            {
                await session.SendAsync(new NickCheckAckMessage(true));
                return;
            }
            var historyEntry = new NicknameHistoryDto
            {
                AccountId = (int)actor.Account.Id,
                OldName = actor.Account.Nickname,
                NewNickname = message.Nickname
            };
            switch (ticket.ItemNumber)
            {
                case 4000001:
                    historyEntry.ExpireDate = -1;
                    if (await ChangeNickname(session.Player, historyEntry, false))
                        actor.Inventory.RemoveOrDecrease(ticket);
                    break;
                case 4000003:
                    historyEntry.ExpireDate = DateTimeOffset.Now.AddDays(1).ToUnixTimeSeconds();
                    if (await ChangeNickname(session.Player, historyEntry, false))
                        actor.Inventory.RemoveOrDecrease(ticket);
                    break;
                case 4000004:
                    historyEntry.ExpireDate = DateTimeOffset.Now.AddDays(7).ToUnixTimeSeconds();
                    if (await ChangeNickname(session.Player, historyEntry, false))
                        actor.Inventory.RemoveOrDecrease(ticket);
                    break;
                case 4000005:
                    historyEntry.ExpireDate = DateTimeOffset.Now.AddDays(30).ToUnixTimeSeconds();
                    if (await ChangeNickname(session.Player, historyEntry, false))
                        actor.Inventory.RemoveOrDecrease(ticket);
                    break;
                default:
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
            }
        }
        private static string NormalizeImportuneText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        private static string BuildImportuneItemLabel(GameShopItemDto item)
        {
            if (item == null)
                return "item";
            return $"{item.ItemNumber} {item.PeriodType} {item.Period}";
        }
        private static string NormalizeGiftText(string text)
        {
            return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        }
        private static Network.Data.Chat.NoteGiftDto CreateGiftDto(GameShopItemDto item)
        {
            var effect = (uint)(item?.Effect ?? 0);
            return new Network.Data.Chat.NoteGiftDto
            {
                Unk1 = 0,
                Unk2 = 0,
                Text = "",
                ItemNumber = item?.ItemNumber ?? 0,
                PriceType = item?.PriceType ?? 0,
                PeriodType = item?.PeriodType ?? 0,
                Period = item?.Period ?? 0,
                Color = item?.Color ?? 0,
                Flags = unchecked((int)(effect | 0x01000000)),
                Mode = 1
            };
        }
        private static Network.Data.Chat.NoteGiftDto CreateRequestGiftDto(GameShopItemDto item, int requestMode)
        {
            var normalizedRequestMode = requestMode > 0 ? requestMode : 6;
            var effect = (int)(item?.Effect ?? 0);
            return new Network.Data.Chat.NoteGiftDto
            {
                Unk1 = 0,
                Unk2 = 0,
                Text = "",
                ItemNumber = item?.ItemNumber ?? 0,
                PriceType = item?.PriceType ?? 0,
                PeriodType = item?.PeriodType ?? 0,
                Period = item?.Period ?? 0,
                Color = item?.Color ?? 0,
                Flags = effect | unchecked((int)0x01000000),
                Mode = (byte)normalizedRequestMode
            };
        }
        private static bool TryResolveGiftItem(GameShopItemDto item, out Shop.ShopItemInfo shopItemInfo,
            out Shop.ShopPrice price, out List<EffectNumber> itemEffects)
        {
            shopItemInfo = null;
            price = null;
            itemEffects = new List<EffectNumber>();
            if (item == null || item.ItemNumber.Id <= 0 || item.Period < 0 || item.Color > 100)
                return false;
            var shop = GameServer.Instance.ResourceCache.GetShop();
            shopItemInfo = shop.GetItemInfo(item.ItemNumber, item.PriceType);
            if (shopItemInfo == null || shopItemInfo.ShopInfoType == 0)
                return false;
            price = shopItemInfo.PriceGroup.GetPrice(item.PeriodType, item.Period);
            if (price == null || !price.IsEnabled)
                return false;
            if (item.Color > shopItemInfo.ShopItem.ColorGroup)
                return false;
            if (item.Effect != 0)
            {
                if (shopItemInfo.EffectGroup.MainEffect != item.Effect)
                    return false;
                foreach (var effect in shopItemInfo.EffectGroup.Effects)
                    itemEffects.Add(effect.Effect);
            }
            else
            {
                itemEffects.Add(0);
            }
            return true;
        }
        [MessageHandler(typeof(NoteGiftItemReqMessage))]
        public async Task NoteGiftItemReq(GameSession session, NoteGiftItemReqMessage message)
        {
            var player = session.Player;
            if (player == null)
                return;
            var senderNick = player.Account?.Nickname ?? "";
            var receiver = NormalizeGiftText(message.Receiver);
            var title = NormalizeGiftText(message.Title) ?? "Gift";
            var body = NormalizeGiftText(message.Message) ?? "";
            Logger.ForAccount(session).Information(
                "[GIFT SEND] from={sender} accId={accountId} nick=\"{nick}\" receiver=\"{receiver}\" title=\"{title}\" item={item} unk7={unk7}",
                senderNick,
                message.AccountId,
                message.Nickname ?? "",
                receiver ?? "",
                title,
                BuildImportuneItemLabel(message.shopItem),
                message.Unk7);
            if (string.IsNullOrEmpty(receiver) ||
                receiver.Equals(senderNick, StringComparison.OrdinalIgnoreCase))
            {
                await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckFailValue));
                return;
            }
            if (!TryResolveGiftItem(message.shopItem, out var shopItemInfo, out var price, out var itemEffects))
            {
                await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckFailValue));
                return;
            }
            if (player.Level < shopItemInfo.ShopItem.MinLevel)
            {
                await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckLevelLowValue));
                return;
            }
            switch (shopItemInfo.PriceGroup.PriceType)
            {
                case ItemPriceType.PEN:
                    if (player.PEN < price.Price)
                    {
                        await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckShortApValue));
                        return;
                    }
                    player.PEN -= (uint)price.Price;
                    break;
                case ItemPriceType.AP:
                case ItemPriceType.Premium:
                    if (player.AP < price.Price)
                    {
                        await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckShortApValue));
                        return;
                    }
                    player.AP -= (uint)price.Price;
                    break;
            }
            var gift = CreateGiftDto(message.shopItem);
            gift.Unk1 = (uint)player.Account.Id;
            gift.Text = body;
            Logger.ForAccount(session).Information(
                "[GIFT SEND DTO] from={sender} to={receiver} dtoSenderLow:{dtoSenderLow} dtoSenderHigh:{dtoSenderHigh} item:{item} priceType:{priceType} periodType:{periodType} period:{period} color:{color} flags:{flags} mode:{mode} body:{body}",
                senderNick,
                receiver,
                gift.Unk1,
                gift.Unk2,
                (uint)gift.ItemNumber,
                (int)gift.PriceType,
                (int)gift.PeriodType,
                gift.Period,
                gift.Color,
                gift.Flags,
                gift.Mode,
                gift.Text ?? "");
            if (!await player.Mailbox.SendGiftAsync(receiver, title, body, gift))
            {
                switch (shopItemInfo.PriceGroup.PriceType)
                {
                    case ItemPriceType.PEN:
                        player.PEN += (uint)price.Price;
                        break;
                    case ItemPriceType.AP:
                    case ItemPriceType.Premium:
                        player.AP += (uint)price.Price;
                        break;
                }
                await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckFailValue));
                return;
            }
            await session.SendAsync(new NoteGiftItemAckMessage(NoteGiftItemAckSuccessValue));
            await session.SendAsync(new MoneyRefreshCashInfoAckMessage(player.PEN, player.AP));
            if (message.Unk7 > 0)
            {
                var requestMail = player.Mailbox[(ulong)message.Unk7];
                if (requestMail != null && requestMail.IsRequest)
                {
                    player.Mailbox.Remove(new[] { requestMail.Id });
                    Logger.ForAccount(session).Information(
                        "[GIFT SEND REQUEST DELETE] player={player} mail={mailId}",
                        senderNick,
                        requestMail.Id);
                }
            }
            Logger.ForAccount(session).Information(
                "[GIFT SEND] ok from={sender} to={receiver} item={item}",
                senderNick,
                receiver,
                BuildImportuneItemLabel(message.shopItem));
        }
        [MessageHandler(typeof(NoteImportuneItemReqMessage))]
        public async Task NoteImportuneItemReq(GameSession session, NoteImportuneItemReqMessage message)
        {
            var player = session.Player;
            if (player == null)
                return;
            var senderNick = player.Account?.Nickname ?? "";
            var receiver = NormalizeImportuneText(message.Unk1);
            var title = NormalizeImportuneText(message.Unk3);
            var body = NormalizeImportuneText(message.Unk4);
            Logger.ForAccount(session).Information(
                "[ITEM REQUEST] from={sender} unk1=\"{unk1}\" unk2={unk2} unk3=\"{unk3}\" unk4=\"{unk4}\" unk5={unk5} item={item}",
                senderNick,
                message.Unk1 ?? "",
                message.Unk2,
                message.Unk3 ?? "",
                message.Unk4 ?? "",
                message.Unk5,
                BuildImportuneItemLabel(message.Unk6));
            if (string.IsNullOrEmpty(receiver) ||
                receiver.Equals(senderNick, StringComparison.OrdinalIgnoreCase))
            {
                await session.SendAsync(new NoteImportuneItemAckMessage { Unk = NoteImportuneItemAckFailValue });
                return;
            }
            if (!TryResolveGiftItem(message.Unk6, out _, out _, out _))
            {
                await session.SendAsync(new NoteImportuneItemAckMessage { Unk = NoteImportuneItemAckFailValue });
                return;
            }
            if (string.IsNullOrEmpty(title))
                title = BuildImportuneItemLabel(message.Unk6);
            if (string.IsNullOrEmpty(body))
                body = $"{senderNick} requested {BuildImportuneItemLabel(message.Unk6)}.";
            var requestGift = CreateRequestGiftDto(message.Unk6, message.Unk5);
            requestGift.Unk1 = (uint)player.Account.Id;
            requestGift.Text = body;
            Logger.ForAccount(session).Information(
                "[ITEM REQUEST DTO] from={sender} to={receiver} dtoSenderLow:{dtoSenderLow} dtoSenderHigh:{dtoSenderHigh} item:{item} priceType:{priceType} periodType:{periodType} period:{period} color:{color} flags:{flags} mode:{mode} body:{body}",
                senderNick,
                receiver,
                requestGift.Unk1,
                requestGift.Unk2,
                (uint)requestGift.ItemNumber,
                (int)requestGift.PriceType,
                (int)requestGift.PeriodType,
                requestGift.Period,
                requestGift.Color,
                requestGift.Flags,
                requestGift.Mode,
                requestGift.Text ?? "");
            if (await player.Mailbox.SendRequestAsync(receiver, title, body, requestGift))
            {
                await session.SendAsync(new NoteImportuneItemAckMessage { Unk = NoteImportuneItemAckSuccessValue });
                Logger.ForAccount(session).Information(
                    "[ITEM REQUEST] sent from={sender} to={receiver} title=\"{title}\" item={item} mode={mode}",
                    senderNick,
                    receiver,
                    title,
                    BuildImportuneItemLabel(message.Unk6),
                    message.Unk5);
                return;
            }
            await session.SendAsync(new NoteImportuneItemAckMessage { Unk = NoteImportuneItemAckFailValue });
        }
        [MessageHandler(typeof(NoteGiftItemGainReqMessage))]
        public async Task NoteGiftItemGainReq(GameSession session, NoteGiftItemGainReqMessage message)
        {
            var player = session.Player;
            if (player == null)
                return;
            var mail = player.Mailbox[(ulong)message.Unk];
            Logger.ForAccount(session).Information(
                "[GIFT GAIN REQ] player={player} mail={mailId} found={found}",
                player.Account?.Nickname ?? "",
                message.Unk,
                mail != null);
            if (mail == null || !mail.IsGift)
            {
                await session.SendAsync(new NoteGiftItemGainAckMessage { Unk1 = 1, Unk2 = (ulong)message.Unk });
                return;
            }
            if (mail.OpenedGift || mail.MessageType == 8)
            {
                await session.SendAsync(new NoteGiftItemGainAckMessage { Unk1 = 2, Unk2 = mail.Id });
                Logger.ForAccount(session).Information(
                    "[GIFT GAIN] already opened player={player} mail={mailId} type={type} openedGift={openedGift}",
                    player.Account?.Nickname ?? "",
                    mail.Id,
                    mail.MessageType,
                    mail.OpenedGift);
                return;
            }
            if (!TryResolveGiftItem(new GameShopItemDto
                {
                    ItemNumber = mail.Gift.ItemNumber,
                    PriceType = mail.Gift.PriceType,
                    PeriodType = mail.Gift.PeriodType,
                    Period = mail.Gift.Period,
                    Color = mail.Gift.Unk5,
                    Effect = mail.Gift.Effect
                },
                out var shopItemInfo,
                out var price,
                out var itemEffects))
            {
                await session.SendAsync(new NoteGiftItemGainAckMessage { Unk1 = 1, Unk2 = mail.Id });
                return;
            }
            Logger.ForAccount(session).Information(
                "[GIFT GAIN MAIL] player={player} mail={mailId} type={type} openedGift={openedGift} sender={sender} receiverId={receiverId} item:{item} priceType:{priceType} periodType:{periodType} period:{period} color:{color} flags:{flags} mode:{mode} body:{body}",
                player.Account?.Nickname ?? "",
                mail.Id,
                mail.MessageType,
                mail.OpenedGift,
                mail.Sender ?? "",
                mail.ReceiverId,
                (uint)mail.Gift.ItemNumber,
                (int)mail.Gift.PriceType,
                (int)mail.Gift.PeriodType,
                mail.Gift.Period,
                mail.Gift.Color,
                mail.Gift.Flags,
                mail.Gift.Mode,
                mail.Message ?? "");
            PlayerItem stackitem = null;
            var stacked = false;
            switch (mail.Gift.PeriodType)
            {
                case ItemPeriodType.Units:
                    stackitem = player.Inventory.FirstOrDefault(x =>
                        x.ItemNumber == mail.Gift.ItemNumber && x.Color == mail.Gift.Unk5);
                    if (stackitem != null)
                    {
                        stackitem.Count += mail.Gift.Period;
                        stackitem.NeedsToSave = true;
                        stacked = true;
                        ShopService.UpdateItemInDB(player, stackitem);
                    }
                    break;
                case ItemPeriodType.Days:
                    stackitem = player.Inventory.FirstOrDefault(x =>
                        x.ItemNumber == mail.Gift.ItemNumber && x.Color == mail.Gift.Unk5);
                    if (stackitem != null)
                    {
                        stackitem.DaysLeft += mail.Gift.Period;
                        stackitem.NeedsToSave = true;
                        stacked = true;
                        ShopService.UpdateItemInDB(player, stackitem);
                    }
                    break;
                case ItemPeriodType.Hours:
                    stackitem = player.Inventory.FirstOrDefault(x =>
                        x.ItemNumber == mail.Gift.ItemNumber && x.Color == mail.Gift.Unk5);
                    if (stackitem != null)
                    {
                        stackitem.HoursLeft += mail.Gift.Period;
                        stackitem.NeedsToSave = true;
                        stacked = true;
                        ShopService.UpdateItemInDB(player, stackitem);
                    }
                    break;
            }
            if (!stacked)
            {
                var created = player.Inventory.Create(shopItemInfo, price, mail.Gift.Unk5, itemEffects.ToArray(),
                    (uint)(price.PeriodType == ItemPeriodType.Units ? price.Period : 0));
                ShopService.AddItemInDB(player, created);
            }
            else
            {
                await player.SendAsync(new ItemUpdateInventoryAckMessage(
                    InventoryAction.Update,
                    stackitem.Map<PlayerItem, ItemDto>()));
            }
            mail.MarkGiftOpened();
            Logger.ForAccount(session).Information(
                "[GIFT GAIN AFTER] player={player} mail={mailId} type={type} openedGift={openedGift}",
                player.Account?.Nickname ?? "",
                mail.Id,
                mail.MessageType,
                mail.OpenedGift);
            await session.SendAsync(new NoteGiftItemGainAckMessage { Unk1 = 0, Unk2 = mail.Id });
            using (var db = GameDatabase.Open())
            {
                player.Inventory.Save(db);
            }
            Logger.ForAccount(session).Information("[GIFT GAIN] ok player={player} mail={mailId} item={item}",
                player.Account?.Nickname ?? "", mail.Id, mail.Gift.ItemNumber);
        }
        [MessageHandler(typeof(CPromotionNewYearCardUseReqMessage))]
        public async Task NewYearSummerEvent(GameSession session, CPromotionNewYearCardUseReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            actor.Inventory.CreateUnits(6000053, 20);
            actor.Inventory.RemoveOrDecreaseCount(actor.Inventory.FirstOrDefault(x => x.ItemNumber == 6000053), 10);
            await actor.SendAsync(new CPromotionNewYearCardUseAckMessage(1));
        }
        [MessageHandler(typeof(CardGambleReqMessage))]
        public async Task GetCardGamble(GameSession session, CardGambleReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            var gambleCards = new ItemNumber[] { 8020000, 8020001, 8020002, 8020003, 8020004, 8020005, 8020006,
             8020007, 8020008, 8020009, 8020010};
            foreach (var cardId in gambleCards)
            {
                var owned = actor.Inventory.FirstOrDefault(x => x.ItemNumber == cardId);
                if (owned != null)
                    actor.Inventory.RemoveOrDecrease(owned);
            }
            actor.Inventory.CreateUnits(4031187, 1);
            await session.SendAsync(new CardGambleAckMessage { Result = 3, ItemId = 4031187 });
        }
        [MessageHandler(typeof(AlchemyCombinatioReqMessage))]
        public async Task AlchemyCombinationHandle(GameSession session, AlchemyCombinatioReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            if (actor.PEN < 2000)
            {
                await session.SendAsync(new AlchemyCombinationAckMessage { Unk = 1 });
                return;
            }
            actor.PEN -= 2000;
            ItemNumber grantedItem = 0;
            switch (message.Id)
            {
                case 1:
                    grantedItem = 6010001;
                    actor.Inventory.CreateUnits(grantedItem, 1);
                    break;
                case 2:
                    grantedItem = 6010002;
                    actor.Inventory.CreateUnits(grantedItem, 1);
                    break;
                case 3:
                    grantedItem = 6010003;
                    actor.Inventory.CreateUnits(grantedItem, 1);
                    break;
                case 4:
                    grantedItem = 6010004;
                    actor.Inventory.CreateUnits(grantedItem, 1);
                    break;
                case 5:
                    grantedItem = 2020039;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 6:
                    grantedItem = 2050007;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 7:
                    grantedItem = 2020009;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 8:
                    grantedItem = 2050008;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 9:
                    grantedItem = 2010041;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 10:
                    grantedItem = 2000007;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 11:
                    grantedItem = 2000027;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 12:
                    grantedItem = 2030008;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 13:
                    grantedItem = 1070004;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                case 14:
                    grantedItem = 2020012;
                    actor.Inventory.Create(grantedItem, 0, 0, new EffectNumber[0], 1, 0);
                    break;
                default:
                    break;
            }
            foreach (var gear in message.Info)
                actor.Inventory.RemoveOrDecreaseCount(actor.Inventory.FirstOrDefault(x => x.ItemNumber == gear.GearId), (uint)gear.GearCount);
            var resultItems = new List<AlchemyItemDto>();
            var resultEntry = new AlchemyItemDto
            {
                Unk = 0,
                itemNumber = grantedItem,
                itemPriceType = ItemPriceType.AP,
                itemPeriodType = ItemPeriodType.None,
                Period = 1,
                Unk3 = 0,
                Color = 0,
                Effect = 0
            };
            resultItems.Add(resultEntry);
            await actor.SendAsync(new AlchemyCombinationAckMessage { Unk = 10, Info = resultItems.ToArray() });
            await actor.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = actor.PEN, AP = actor.AP });
        }
        [MessageHandler(typeof(AlchemyDecompositionReqMessage))]
        public async Task AlchemyDecompositionHandle(GameSession session, AlchemyDecompositionReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            var target = actor.Inventory.Where(x => x.Id == message.ID).FirstOrDefault();
            var secureRng = new SecureRandom();
            if (target.PeriodType != ItemPeriodType.Days)
                actor.Inventory.Remove(target);
            else
                actor.Inventory.RemoveOrDecreaseDays(target, (ushort)message.Days);
            if (target.PeriodType != ItemPeriodType.Days)
            {
                actor.Inventory.CreateUnits(6010003, 1);
                actor.Inventory.CreateUnits(6010004, 1);
                if (secureRng.Next(1, 101) > 80)
                    actor.Inventory.CreateUnits(6010101, 1);
                else if (secureRng.Next(1, 101) > 35 && secureRng.Next(1, 101) < 60)
                    actor.Inventory.CreateUnits(6010102, 1);
                await session.SendAsync(new AlchemyDecompositionAckMessage(9, 48649, 1, 1, 6010004, 0));
            }
            else
            {
                var gemResult = 0;
                if (message.Days >= 30 && message.Days <= 40)
                {
                    gemResult = 6010000;
                    actor.Inventory.CreateUnits(6010000, 1);
                }
                else if (message.Days >= 40 && message.Days <= 61)
                {
                    gemResult = 6010001;
                    actor.Inventory.CreateUnits(6010000, 1);
                    actor.Inventory.CreateUnits(6010001, 1);
                }
                else if (message.Days >= 61 && message.Days <= 71)
                {
                    gemResult = 6010001;
                    actor.Inventory.CreateUnits(6010001, 1);
                }
                else if (message.Days >= 71 && message.Days <= 80)
                {
                    gemResult = 6010002;
                    actor.Inventory.CreateUnits(6010002, 1);
                }
                else if (message.Days >= 80 && message.Days <= 101)
                {
                    gemResult = 6010002;
                    actor.Inventory.CreateUnits(6010000, 1);
                    actor.Inventory.CreateUnits(6010002, 1);
                }
                else if (message.Days >= 102 && message.Days <= 131)
                {
                    gemResult = 6010002;
                    actor.Inventory.CreateUnits(6010001, 1);
                    actor.Inventory.CreateUnits(6010002, 1);
                }
                else if (message.Days >= 132 && message.Days <= 140)
                {
                    gemResult = 6010003;
                    actor.Inventory.CreateUnits(6010003, 1);
                }
                else if (message.Days >= 141 && message.Days <= 161)
                {
                    gemResult = 6010003;
                    actor.Inventory.CreateUnits(6010000, 1);
                    actor.Inventory.CreateUnits(6010003, 1);
                }
                else if (message.Days >= 161 && message.Days <= 201)
                {
                    gemResult = 6010003;
                    actor.Inventory.CreateUnits(6010001, 1);
                    actor.Inventory.CreateUnits(6010003, 1);
                }
                else if (message.Days >= 202 && message.Days <= 210)
                {
                    gemResult = 6010003;
                    actor.Inventory.CreateUnits(6010002, 1);
                    actor.Inventory.CreateUnits(6010003, 1);
                }
                else if (message.Days >= 202 && message.Days <= 210)
                {
                    gemResult = 6010004;
                    actor.Inventory.CreateUnits(6010004, 1);
                }
                else if (message.Days >= 221 && message.Days <= 241)
                {
                    gemResult = 6010004;
                    actor.Inventory.CreateUnits(6010000, 1);
                    actor.Inventory.CreateUnits(6010004, 1);
                }
                else if (message.Days >= 242 && message.Days <= 281)
                {
                    gemResult = 6010004;
                    actor.Inventory.CreateUnits(6010001, 1);
                    actor.Inventory.CreateUnits(6010004, 1);
                }
                else if (message.Days >= 282 && message.Days <= 341)
                {
                    gemResult = 6010004;
                    actor.Inventory.CreateUnits(6010002, 1);
                    actor.Inventory.CreateUnits(6010004, 1);
                }
                else if (message.Days >= 342)
                {
                    gemResult = 6010004;
                    actor.Inventory.CreateUnits(6010003, 1);
                    actor.Inventory.CreateUnits(6010004, 1);
                }
                await session.SendAsync(new AlchemyDecompositionAckMessage(9, 48649, 1, 1, gemResult, 0));
            }
            await actor.SendAsync(new MoneyRefreshPenInfoAckMessage { Unk = actor.PEN });
        }
        [MessageHandler(typeof(DailyMissionResetReqMessage))]
        public async Task DailyMissionResetReq(GameSession session, DailyMissionResetReqMessage message)
        {
            if (session.Player == null)
                return;
            if (session.Player.PEN < 600)
            {
                await session.SendAsync(new EnchantEnchantItemAckMessage(EnchantResult.NotEnoughMoney));
                return;
            }
            using (var db = GameDatabase.Open())
            {
                var date = DateTime.Now.ToString("dddd, dd MMMM yyyy");
                var missionRow = DbUtil.Find<Daily_MissionDto>(db, statement => statement
                    .Where($"{nameof(Daily_MissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)} AND ({nameof(Daily_MissionDto.Date):C} = @{nameof(date)})")
                    .WithParameters(new { session.Player.Account.Id, date })).FirstOrDefault();
                var rng = new SecureRandom();
                if (missionRow != null)
                {
                    session.Player.PEN -= 600;
                    await session.SendAsync(new MoneyRefreshCashInfoAckMessage(session.Player.PEN, session.Player.AP));
                    int rolledMap = rng.Next(0, 13);
                    missionRow.Map = rolledMap;
                    DbUtil.Update(db, missionRow);
                    await session.Player?.SendAsync(new DailyMission_NoticeMessage { Unk = 2, GameMode = 0, Map = rolledMap, MaxProgress = missionRow.MaxProgress, Progress = 0, Unk5 = 5, Unk6 = new int[] { missionRow.Reward, missionRow.Reward2, missionRow.Reward3 } });
                }
            }
        }
        [MessageHandler(typeof(DailyMissionNextStepReqMessage))]
        public async Task DailyMissionNextStepReq(GameSession session, DailyMissionNextStepReqMessage message)
        {
            if (session.Player == null)
                return;
            using (var db = GameDatabase.Open())
            {
                var date = DateTime.Now.ToString("dddd, dd MMMM yyyy");
                var missionRow = DbUtil.Find<Daily_MissionDto>(db, statement => statement
                    .Where($"{nameof(Daily_MissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)} AND ({nameof(Daily_MissionDto.Date):C} = @{nameof(date)})")
                    .WithParameters(new { session.Player.Account.Id, date })).FirstOrDefault();
                if (missionRow != null)
                {
                    var nextStep = Math.Min((missionRow.Progress <= 0 ? message.unk1 : missionRow.Progress) + 1, 3);
                    missionRow.Progress = nextStep;
                    DbUtil.Update(db, missionRow);
                    await session.Player?.SendAsync(new DailyMission_NoticeMessage { Unk = 1, GameMode = 0, Map = missionRow.Map, MaxProgress = nextStep, Progress = 0, Unk5 = 5, Unk6 = new int[] { missionRow.Reward, missionRow.Reward2, missionRow.Reward3 } });
                }
            }
        }
        [MessageHandler(typeof(Daily_Mission_Reward_ReqMessage))]
        public async Task Daily_Mission_Reward_Req(GameSession session, Daily_Mission_Reward_ReqMessage message)
        {
            if (session.Player == null)
                return;
            using (var db = GameDatabase.Open())
            {
                var date = DateTime.Now.ToString("dddd, dd MMMM yyyy");
                var missionRow = DbUtil.Find<Daily_MissionDto>(db, statement => statement
                    .Where($"{nameof(Daily_MissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)} AND ({nameof(Daily_MissionDto.Date):C} = @{nameof(date)})")
                    .WithParameters(new { session.Player.Account.Id, date })).FirstOrDefault();
                if (missionRow != null && !missionRow.IsRewarded)
                {
                    var rewardIds = new[] { missionRow.Reward, missionRow.Reward2, missionRow.Reward3 };
                    var count = Math.Clamp(message.unk1 > 0 ? message.unk1 : missionRow.Progress, 1, 3);
                    for (var box = 0; box < count; box++)
                    {
                        var rewardId = rewardIds[box];
                        if (rewardId < 1 || rewardId > 9)
                            continue;
                        var itemKey = (uint)(4020055 + rewardId);
                        try
                        {
                            session.Player.Inventory.Create(itemKey, 1, 0, new EffectNumber[0], 1);
                        }
                        catch (Exception ex)
                        {
                        }
                    }
                    missionRow.IsRewarded = true;
                    DbUtil.Update(db, missionRow);
                    await session.Player?.SendAsync(new DailyMissionRewardAckMessage { unk1 = 1 });
                }
            }
        }
        [MessageHandler(typeof(AchieveMissionRewardReqMessage))]
        public async Task AchieveMissionRewardReq(GameSession session, AchieveMissionRewardReqMessage message)
        {
            using (var db = GameDatabase.Open())
            {
                var progressRow = DbUtil.Find<AchieveMissionDto>(db, statement => statement
                .Where($"{nameof(AchieveMissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)}")
                .WithParameters(new { session.Player.Account.Id })).FirstOrDefault();
                var progressCaps = DbUtil.Find<AchieveMissionProgressDto>(db).FirstOrDefault();
                if (progressRow != null && progressCaps != null)
                {
                    if (message.MissionId == 1 && progressRow.Progress < progressCaps.MaxProgress)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 2 && progressRow.Progress2 < progressCaps.MaxProgress2)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 3 && progressRow.Progress3 < progressCaps.MaxProgress3)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 4 && progressRow.Progress4 < progressCaps.MaxProgress4)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 5 && progressRow.Progress5 < progressCaps.MaxProgress5)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 6 && progressRow.Progress6 < progressCaps.MaxProgress6)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 7 && progressRow.Progress7 < progressCaps.MaxProgress7)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 8 && progressRow.Progress8 < progressCaps.MaxProgress8)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 9 && progressRow.Progress9 < progressCaps.MaxProgress9)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                    else if (message.MissionId == 10 && progressRow.Progress10 < progressCaps.MaxProgress10)
                    {
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
                        return;
                    }
                }
                var rewardedRows = DbUtil.Find<AchieveMissionRewardedDto>(db, statement => statement
                .Where($"{nameof(AchieveMissionRewardedDto.PlayerId):C} = @{nameof(session.Player.Account.Id)}")
                .WithParameters(new { session.Player.Account.Id }));
                var rewardDef = DbUtil.Find<AchieveMissionRewardsDto>(db, statement => statement
                .Where($"{nameof(AchieveMissionRewardsDto.Id):C} = @{nameof(message.RewardId)}")
                .WithParameters(new { message.RewardId })).FirstOrDefault();
                if (!rewardedRows.Any())
                {
                    AchieveMissionRewardedDto rewardedEntry = new AchieveMissionRewardedDto
                    {
                        PlayerId = session.Player.Account.Id,
                        MissionId = message.MissionId,
                    };
                    DbUtil.Insert(db, rewardedEntry);
                    if (rewardDef != null)
                        session.Player.Inventory.Create(rewardDef.Reward, 0, rewardDef.Color, new EffectNumber[0], 1, 0);
                    await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 1 });
                    return;
                }
                else
                {
                    var alreadyRewarded = rewardedRows.Any(x => x.MissionId == message.MissionId);
                    if (!alreadyRewarded)
                    {
                        AchieveMissionRewardedDto rewardedEntry = new AchieveMissionRewardedDto
                        {
                            PlayerId = session.Player.Account.Id,
                            MissionId = message.MissionId,
                        };
                        DbUtil.Insert(db, rewardedEntry);
                        if (rewardDef != null)
                        {
                            if (rewardDef.Reward > 0)
                                session.Player.Inventory.Create(rewardDef.Reward, 1, rewardDef.Color, new EffectNumber[0], 0);
                        }
                        await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 1 });
                        return;
                    }
                }
            }
            await session.SendAsync(new AchieveMissionRewardAckMessage { Unk = 0 });
        }
        [MessageHandler(typeof(MoneyRefreshCashInfoReqMessage))]
        public async Task MoneyRefreshCashInfoReq(GameSession session, MoneyRefreshCashInfoReqMessage message)
        {
            if (session.Player == null)
                return;
            await session.SendAsync(new MoneyRefreshCashInfoAckMessage(session.Player.PEN, session.Player.AP));
        }
        [MessageHandler(typeof(PromotionCointEventGetCoinReqMessage))]
        public async Task PromotionCointEventGetCoinReqMessage(GameSession session, PromotionCointEventGetCoinReqMessage message)
        {
            var actor = session.Player;
            actor.AP += 5;
            actor.PEN += 1500;
            await session.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = session.Player.PEN, AP = session.Player.AP });
            await session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, session.Player.Account.Id, "CoinSystem", $"You got 5 AP & 1500 PEN!"));
        }
        [MessageHandler(typeof(AchieveMissionReqMessage))]
        public async Task AchieveMissionReq(GameSession session, AchieveMissionReqMessage message)
        {
            if (session.Player == null)
                return;
            using (var db = GameDatabase.Open())
            {
                var progressRow = DbUtil.Find<AchieveMissionDto>(db, statement => statement
                .Where($"{nameof(AchieveMissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)}")
                .WithParameters(new { session.Player.Account.Id })).FirstOrDefault();
                if (progressRow == null)
                {
                    AchieveMissionDto newRow = new AchieveMissionDto();
                    newRow.PlayerId = session.Player.Account.Id;
                    DbUtil.Insert(db, newRow);
                }
                await session.SendAsync(new AchieveMissionAckMessage
                {
                    Unk = new int[] { progressRow?.Progress ?? 0, progressRow?.Progress2 ?? 0,
                    progressRow?.Progress3 ?? 0, progressRow?.Progress4 ?? 0, progressRow?.Progress5 ?? 0, progressRow?.Progress6 ?? 0,
                    progressRow?.Progress7 ?? 0,progressRow?.Progress8 ?? 0, progressRow?.Progress9 ?? 0, progressRow?.Progress10 ?? 0 },
                    Unk2 = 3
                });
            }
        }
    }
}

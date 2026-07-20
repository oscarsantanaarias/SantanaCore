using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using ExpressMapper.Extensions;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Club;
using Santana.Network.Message.Game;
using Santana.Network.Services;

namespace Santana.Commands
{
    internal class customCommands : ICommand
    {
        public customCommands()
        {
            Name = "custom";
            AllowConsole = true;
            Permission = SecurityLevel.Developer;
            SubCommands = new ICommand[]
            {
                new RoomInviteSub(),
                new WishlistAckSub(),
                new GiftAckSub(),
                new GiftReqSub(),
                new NoteTypeSub(),
                new ClubRecordTestSub(),
                new ClubRecordSub(),
                new ClubStuffTwoSub(),
                new ImportuneAckSub(),
                new ImportuneReqSub()
            };
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            Reply(plr, "Uso:");
            Reply(plr, "custom room_invite <targetNick> <unk1> <nick> [channelId] [roomId] [gameRule] [playerCount] [unk3]");
            Reply(plr, "custom wishlist_ack <targetNick> <unk1> <unk2> [itemId] [priceType] [periodType] [period] [color] [effect] [rowId]");
            Reply(plr, "custom gift_ack <successValue> [failValue] [shortApValue] [levelLowValue]");
            Reply(plr, "custom gift_req <targetNick> <title> <body> [itemId] [priceType] [periodType] [period] [color] [effect] [unk7]");
            Reply(plr, "custom gift_req <senderNick> <targetNick> <title> <body> [itemId] [priceType] [periodType] [period] [color] [effect] [unk7]");
            Reply(plr, "custom note_type <targetNick> <messageType> <title> <body>");
            Reply(plr, "custom note_type <senderNick> <targetNick> <messageType> <title> <body>");
            Reply(plr, "custom clubrecord <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7array> <unk8array> [targetNick] [ackUnk1]");
            Reply(plr, "custom club_record_test <targetNick> <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7csv> <unk8csv> [ackUnk1]");
            Reply(plr, "custom stuff2 <targetNick> <rowNick> <clubRank> <unk1> <unk2> <unk4> <unk5> <unk7> <unk11> <unk12> [server] [channel] [room]");
            Reply(plr, "custom importune_ack <successValue> [failValue]");
            Reply(plr, "custom importune_req <targetNick> <title> <body> [unk2] [unk5] [itemId] [period] [color] [effect]");
            Reply(plr, "custom importune_req <senderNick> <targetNick> <title> <body> [unk2] [unk5] [itemId] [period] [color] [effect]");
            Reply(plr, "Ejemplo:");
            Reply(plr, "custom room_invite tester 4 SafronCity");
            Reply(plr, "custom wishlist_ack SafronCity 0 1 2000076 2 1 1 0 0 1");
            Reply(plr, "custom gift_ack 3 0 1 4");
            Reply(plr, "custom gift_req juan Gift_Test hola_esto_es_gift 2000080 2 1 1 0 0 0");
            Reply(plr, "custom gift_req SafronCity juan Gift_Test hola_esto_es_gift 2000080 2 1 1 0 0 0");
            Reply(plr, "custom note_type juan 4 Club_Test hola_club_manager");
            Reply(plr, "custom note_type SafronCity juan 4 Club_Test hola_club_manager");
            Reply(plr, "custom clubrecord Zen Tiranos 1 2 1 2 {1,2,3,3} {4,5}");
            Reply(plr, "custom club_record_test juan Zen EsperDevs 1 2 1 2 1,1,1 4,7,9");
            Reply(plr, "custom stuff2 juan VY 3 11 22 44 55 77 111 122");
            Reply(plr, "custom importune_ack 2 0");
            Reply(plr, "custom importune_req juan Item_Request hola_esto_es_prueba 4 6 2000080 1 0 0");
            Reply(plr, "custom importune_req SafronCity juan Item_Request hola_esto_es_prueba 4 6 2000080 1 0 0");
            return true;
        }

        public string Help()
        {
            var text = new StringBuilder();
            text.AppendLine(Name);
            foreach (var sub in SubCommands)
                text.AppendLine(sub.Help());
            return text.ToString();
        }

        private class RoomInviteSub : ICommand
        {
            public RoomInviteSub()
            {
                Name = "room_invite";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                try
                {
                    if (args.Length < 3)
                    {
                        Reply(plr, "Uso: custom room_invite <targetNick> <unk1> <nick> [channelId] [roomId] [gameRule] [playerCount] [unk3]");
                        Reply(plr, "Ejemplo: custom room_invite tester 4 SafronCity");
                        return true;
                    }

                    var recipient = LocatePlayer(args[0]);
                    if (recipient == null)
                    {
                        Reply(plr, $"Target '{args[0]}' no esta online.");
                        return true;
                    }

                    if (!ulong.TryParse(args[1], out var headValue)) { Reply(plr, "unk1 invalido."); return true; }
                    var nickText = args[2].Replace('_', ' ');
                    var origin = plr?.Map<Player, PlayerLocationDto>() ?? new PlayerLocationDto();
                    var channelValue = ArgInt(args, 3, origin.ChannelId);
                    var roomValue = ArgInt(args, 4, origin.RoomId);
                    var ruleValue = ArgInt(args, 5, origin.Unk);
                    var countValue = ArgInt(args, 6, origin.GameServerId);
                    var tailValue = ArgInt(args, 7, plr?.Level ?? 0);
                    var groupValue = origin.ServerGroupId;
                    var chatValue = origin.ChatServerId;

                    await recipient.SendAsync(new RoomInvitationPlayerAckMessage
                    {
                        Unk1 = headValue,
                        Unk2 = nickText,
                        Location = new PlayerLocationDto
                        {
                            ServerGroupId = groupValue,
                            ChannelId = channelValue,
                            RoomId = roomValue,
                            Unk = ruleValue,
                            GameServerId = countValue,
                            ChatServerId = chatValue
                        },
                        Unk3 = tailValue
                    });

                    Reply(plr, $"Enviado RoomInvitationPlayerAck a {recipient.Account.Nickname} | unk1={headValue} nick={nickText} sg={groupValue} ch={channelValue} room={roomValue} rule={ruleValue} game={countValue} chat={chatValue} unk3={tailValue}");
                    Console.WriteLine($"[ROOM INVITE TEST] target={recipient.Account.Nickname} unk1={headValue} nick={nickText} sg={groupValue} ch={channelValue} room={roomValue} rule={ruleValue} game={countValue} chat={chatValue} unk3={tailValue}");
                }
                catch (Exception failure)
                {
                    Reply(plr, "ERROR en custom room_invite: " + failure.Message);
                    Console.WriteLine(failure);
                }

                return true;
            }

            public string Help()
            {
                return "room_invite <targetNick> <unk1> <nick> [channelId] [roomId] [gameRule] [playerCount] [unk3]";
            }
        }

        private class GiftAckSub : ICommand
        {
            public GiftAckSub()
            {
                Name = "gift_ack";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 1)
                {
                    Reply(plr, "Uso: custom gift_ack <successValue> [failValue] [shortApValue] [levelLowValue]");
                    Reply(plr, $"Actual: success={GeneralService.NoteGiftItemAckSuccessValue} fail={GeneralService.NoteGiftItemAckFailValue} shortAp={GeneralService.NoteGiftItemAckShortApValue} levelLow={GeneralService.NoteGiftItemAckLevelLowValue}");
                    return true;
                }

                if (!int.TryParse(args[0], out var successCode))
                {
                    Reply(plr, "successValue invalido.");
                    return true;
                }

                var failCode = args.Length > 1 && int.TryParse(args[1], out var parsedFail)
                    ? parsedFail
                    : GeneralService.NoteGiftItemAckFailValue;
                var shortApCode = args.Length > 2 && int.TryParse(args[2], out var parsedAp)
                    ? parsedAp
                    : GeneralService.NoteGiftItemAckShortApValue;
                var levelLowCode = args.Length > 3 && int.TryParse(args[3], out var parsedLvl)
                    ? parsedLvl
                    : GeneralService.NoteGiftItemAckLevelLowValue;

                GeneralService.NoteGiftItemAckSuccessValue = successCode;
                GeneralService.NoteGiftItemAckFailValue = failCode;
                GeneralService.NoteGiftItemAckShortApValue = shortApCode;
                GeneralService.NoteGiftItemAckLevelLowValue = levelLowCode;

                Reply(plr, $"NoteGiftItemAck values -> success={successCode} fail={failCode} shortAp={shortApCode} levelLow={levelLowCode}");
                return true;
            }

            public string Help()
            {
                return "gift_ack <successValue> [failValue] [shortApValue] [levelLowValue]";
            }
        }

        private class WishlistAckSub : ICommand
        {
            public WishlistAckSub()
            {
                Name = "wishlist_ack";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 3)
                {
                    Reply(plr, "Uso: custom wishlist_ack <targetNick> <unk1> <unk2> [itemId] [priceType] [periodType] [period] [color] [effect] [rowId]");
                    Reply(plr, "Ejemplo: custom wishlist_ack SafronCity 0 1 2000076 2 1 1 0 0 1");
                    return true;
                }

                var recipient = LocatePlayer(args[0]);
                if (recipient?.Session == null)
                {
                    Reply(plr, $"Target '{args[0]}' no esta ingame.");
                    return true;
                }

                if (!int.TryParse(args[1], out var headValue))
                {
                    Reply(plr, "unk1 invalido.");
                    return true;
                }

                if (!byte.TryParse(args[2], out var flagValue))
                {
                    Reply(plr, "unk2 invalido.");
                    return true;
                }

                var itemNumberArg = ArgUInt(args, 3, 2000076);
                var priceKind = ArgUInt(args, 4, 2);
                var periodKind = ArgUInt(args, 5, 1);
                var periodLen = ArgUShort(args, 6, 1);
                var colorValue = ArgByte(args, 7, 0);
                var effectValue = ArgUInt(args, 8, 0);
                var rowValue = args.Length > 9 && ulong.TryParse(args[9], out var parsedRow)
                    ? parsedRow
                    : 1UL;

                var basket = new Network.Data.Game.ShoppingBasketDto
                {
                    ItemId = rowValue,
                    ShopItem = new Network.Data.Game.ShopItemDto
                    {
                        ItemNumber = itemNumberArg,
                        PriceType = (ItemPriceType)priceKind,
                        PeriodType = (ItemPeriodType)periodKind,
                        Period = periodLen,
                        Color = colorValue,
                        Effect = effectValue
                    }
                };

                await recipient.Session.SendAsync(new ShoppingBasketActionAckMessage(headValue, flagValue, basket));
                Reply(plr, $"Wishlist ack enviado target={recipient.Account.Nickname} unk1={headValue} unk2={flagValue} rowId={rowValue} item={itemNumberArg}");
                return true;
            }

            public string Help()
            {
                return "wishlist_ack <targetNick> <unk1> <unk2> [itemId] [priceType] [periodType] [period] [color] [effect] [rowId]";
            }
        }

        private class GiftReqSub : ICommand
        {
            public GiftReqSub()
            {
                Name = "gift_req";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 3)
                {
                    Reply(plr, "Uso player: custom gift_req <targetNick> <title> <body> [itemId] [priceType] [periodType] [period] [color] [effect] [unk7]");
                    Reply(plr, "Uso consola: custom gift_req <senderNick> <targetNick> <title> <body> [itemId] [priceType] [periodType] [period] [color] [effect] [unk7]");
                    return true;
                }

                Player sender = plr;
                int shift = 0;

                if (sender?.Session == null)
                {
                    if (args.Length < 4)
                    {
                        Reply(plr, "Con consola necesita senderNick y targetNick.");
                        return true;
                    }

                    sender = LocatePlayer(args[0]);
                    if (sender?.Session == null)
                    {
                        Reply(plr, $"Sender '{args[0]}' no esta ingame.");
                        return true;
                    }

                    shift = 1;
                }
                else if (args.Length >= 4 &&
                         args[0].Equals(sender.Account?.Nickname ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    shift = 1;
                }

                var targetNick = args[shift + 0].Replace('_', ' ');
                var titleText = args[shift + 1].Replace('_', ' ');
                var bodyText = args[shift + 2].Replace('_', ' ');
                var itemNumberArg = ArgUInt(args, shift + 3, 2000080);
                var priceKind = ArgUInt(args, shift + 4, 2);
                var periodKind = ArgUInt(args, shift + 5, 1);
                var periodLen = ArgUShort(args, shift + 6, 1);
                var colorValue = ArgByte(args, shift + 7, 0);
                var effectValue = ArgUInt(args, shift + 8, 0);
                var trailingValue = ArgLong(args, shift + 9, 0);

                var catalog = GameServer.Instance.ResourceCache.GetShop();
                var itemKey = (ItemNumber)itemNumberArg;
                var itemInfo = catalog.GetItemInfo(itemKey, (ItemPriceType)priceKind) ?? catalog.GetItemInfo(itemKey);
                if (itemInfo == null)
                {
                    Reply(plr, $"Item invalido: {itemNumberArg}");
                    return true;
                }

                var priceEntry = itemInfo.PriceGroup.GetPrice((ItemPeriodType)periodKind, periodLen);
                if (priceEntry == null || !priceEntry.IsEnabled)
                {
                    var enabledPrice = itemInfo.PriceGroup.Prices.FirstOrDefault(entry => entry.IsEnabled);
                    if (enabledPrice == null)
                    {
                        Reply(plr, $"Item sin precio habilitado: {itemNumberArg}");
                        return true;
                    }

                    priceKind = (uint)itemInfo.PriceGroup.PriceType;
                    periodKind = (uint)enabledPrice.PeriodType;
                    periodLen = enabledPrice.Period;
                }
                else
                {
                    priceKind = (uint)itemInfo.PriceGroup.PriceType;
                    periodKind = (uint)priceEntry.PeriodType;
                    periodLen = priceEntry.Period;
                }

                var giftMessage = new NoteGiftItemReqMessage
                {
                    AccountId = sender.Account.Id,
                    Nickname = sender.Account.Nickname,
                    Receiver = targetNick,
                    Title = titleText,
                    Message = bodyText,
                    shopItem = new Network.Data.Game.ShopItemDto
                    {
                        ItemNumber = itemNumberArg,
                        PriceType = (ItemPriceType)priceKind,
                        PeriodType = (ItemPeriodType)periodKind,
                        Period = periodLen,
                        Color = colorValue,
                        Effect = effectValue
                    },
                    Unk7 = trailingValue
                };

                await new GeneralService().NoteGiftItemReq(sender.Session, giftMessage);
                Reply(plr, $"Gift req enviado sender={sender.Account.Nickname} target={targetNick} item={itemNumberArg} priceType={priceKind} periodType={periodKind} period={periodLen}");
                return true;
            }

            public string Help()
            {
                return "gift_req [senderNick] <targetNick> <title> <body> [itemId] [priceType] [periodType] [period] [color] [effect] [unk7]";
            }
        }

        private class NoteTypeSub : ICommand
        {
            public NoteTypeSub()
            {
                Name = "note_type";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 4)
                {
                    Reply(plr, "Uso player: custom note_type <targetNick> <messageType> <title> <body>");
                    Reply(plr, "Uso consola: custom note_type <senderNick> <targetNick> <messageType> <title> <body>");
                    return true;
                }

                Player sender = plr;
                int shift = 0;

                if (sender?.Mailbox == null)
                {
                    if (args.Length < 5)
                    {
                        Reply(plr, "Con consola necesita senderNick y targetNick.");
                        return true;
                    }

                    sender = LocatePlayer(args[0]);
                    if (sender?.Mailbox == null)
                    {
                        Reply(plr, $"Sender '{args[0]}' no esta ingame.");
                        return true;
                    }

                    shift = 1;
                }
                else if (args.Length >= 5 &&
                         args[0].Equals(sender.Account?.Nickname ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                {
                    shift = 1;
                }

                var targetNick = args[shift + 0].Replace('_', ' ');
                var noteKind = ArgInt(args, shift + 1, 0);
                var titleText = args[shift + 2].Replace('_', ' ');
                var bodyText = args[shift + 3].Replace('_', ' ');

                var delivered = await sender.Mailbox.SendTypedAsync(targetNick, titleText, bodyText, noteKind);
                Reply(plr, $"Note type enviado sender={sender.Account.Nickname} target={targetNick} type={noteKind} sent={delivered}");
                return true;
            }

            public string Help()
            {
                return "note_type [senderNick] <targetNick> <messageType> <title> <body>";
            }
        }

        private class ClubRecordTestSub : ICommand
        {
            public ClubRecordTestSub()
            {
                Name = "club_record_test";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 9)
                {
                    Reply(plr, "Uso: custom club_record_test <targetNick> <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7csv> <unk8csv> [ackUnk1]");
                    Reply(plr, "Ejemplo: custom club_record_test juan Zen EsperDevs 1 2 1 2 1,1,1 4,7,9");
                    Reply(plr, "CSV: usa coma. '-' o '_' = array vacio.");
                    return true;
                }

                var recipient = LocatePlayer(args[0]);
                if (recipient?.Session == null)
                {
                    Reply(plr, $"Target '{args[0]}' no esta ingame.");
                    return true;
                }

                var rivalName = args[1].Replace('_', ' ');
                var clubText = args[2].Replace('_', ' ');
                var modeValue = ArgInt(args, 3, 1);
                var mapValue = ArgInt(args, 4, 2);
                var winValue = ArgInt(args, 5, 0);
                var sixthValue = ArgInt(args, 6, 2);
                var seventhList = SplitCsv(args[7]);
                var eighthList = SplitCsv(args[8]);
                var ackValue = ArgInt(args, 9, 0);

                await recipient.SendAsync(new ClubNotice_Record_Refresh_Ack
                {
                    Unk1 = ackValue,
                    Info = new[]
                    {
                        new ClubNoticeRecordDto
                        {
                            Unk1 = rivalName,
                            Unk2 = clubText,
                            Unk3 = modeValue,
                            Unk4 = mapValue,
                            Unk5 = winValue,
                            Unk6 = sixthValue,
                            Unk7 = seventhList,
                            Unk8 = eighthList
                        }
                    }
                });

                Reply(plr, $"ClubRecord test -> {recipient.Account.Nickname} rival={rivalName} club={clubText} mode={modeValue} map={mapValue} win={winValue} u6={sixthValue} u7=[{string.Join(",", seventhList)}] u8=[{string.Join(",", eighthList)}] ack={ackValue}");
                Console.WriteLine($"[ClubRecordTest] target={recipient.Account.Nickname} rival={rivalName} club={clubText} mode={modeValue} map={mapValue} win={winValue} u6={sixthValue} u7={string.Join(",", seventhList)} u8={string.Join(",", eighthList)} ack={ackValue}");
                return true;
            }

            public string Help()
            {
                return "club_record_test <targetNick> <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7csv> <unk8csv> [ackUnk1]";
            }
        }

        private class ClubRecordSub : ICommand
        {
            public ClubRecordSub()
            {
                Name = "clubrecord";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 8)
                {
                    Reply(plr, "Uso player: custom clubrecord <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7array> <unk8array> [ackUnk1]");
                    Reply(plr, "Uso consola: custom clubrecord <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7array> <unk8array> <targetNick> [ackUnk1]");
                    Reply(plr, "Ejemplo: custom clubrecord Zen Tiranos 1 2 1 2 {1,2,3,3} {4,5}");
                    return true;
                }

                var recipient = plr;
                var ackSlot = 8;
                if (recipient?.Session == null)
                {
                    if (args.Length < 9)
                    {
                        Reply(plr, "Con consola necesita targetNick.");
                        return true;
                    }

                    recipient = LocatePlayer(args[8]);
                    ackSlot = 9;
                }
                else if (args.Length >= 9)
                {
                    var namedTarget = LocatePlayer(args[8]);
                    if (namedTarget?.Session != null)
                    {
                        recipient = namedTarget;
                        ackSlot = 9;
                    }
                }

                if (recipient?.Session == null)
                {
                    Reply(plr, "Target no esta ingame.");
                    return true;
                }

                var rivalName = args[0].Replace('_', ' ');
                var clubText = args[1].Replace('_', ' ');
                var modeValue = ArgInt(args, 2, 1);
                var mapValue = ArgInt(args, 3, 2);
                var winValue = ArgInt(args, 4, 0);
                var sixthValue = ArgInt(args, 5, 2);
                var seventhList = SplitCsv(args[6]);
                var eighthList = SplitCsv(args[7]);
                var ackValue = ArgInt(args, ackSlot, 0);

                await recipient.SendAsync(new ClubNotice_Record_Refresh_Ack
                {
                    Unk1 = ackValue,
                    Info = new[]
                    {
                        new ClubNoticeRecordDto
                        {
                            Unk1 = rivalName,
                            Unk2 = clubText,
                            Unk3 = modeValue,
                            Unk4 = mapValue,
                            Unk5 = winValue,
                            Unk6 = sixthValue,
                            Unk7 = seventhList,
                            Unk8 = eighthList
                        }
                    }
                });

                Reply(plr, $"ClubRecord -> {recipient.Account.Nickname} rival={rivalName} club={clubText} mode={modeValue} map={mapValue} win={winValue} u6={sixthValue} u7=[{string.Join(",", seventhList)}] u8=[{string.Join(",", eighthList)}] ack={ackValue}");
                Console.WriteLine($"[ClubRecord] target={recipient.Account.Nickname} rival={rivalName} club={clubText} mode={modeValue} map={mapValue} win={winValue} u6={sixthValue} u7={string.Join(",", seventhList)} u8={string.Join(",", eighthList)} ack={ackValue}");
                return true;
            }

            public string Help()
            {
                return "clubrecord <rival> <clubName> <mode> <mapId> <win> <unk6> <unk7array> <unk8array> [targetNick] [ackUnk1]";
            }
        }

        private class ClubStuffTwoSub : ICommand
        {
            public ClubStuffTwoSub()
            {
                Name = "stuff2";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 10)
                {
                    Reply(plr, "Uso: custom stuff2 <targetNick> <rowNick> <clubRank> <unk1> <unk2> <unk4> <unk5> <unk7> <unk11> <unk12> [server] [channel] [room]");
                    Reply(plr, "Ejemplo: custom stuff2 juan VY 3 11 22 44 55 77 111 122");
                    return true;
                }

                var recipient = LocatePlayer(args[0]);
                if (recipient?.Session == null)
                {
                    Reply(plr, $"Target '{args[0]}' no esta ingame.");
                    return true;
                }

                var rowName = args[1].Replace('_', ' ');
                var memberAccountId = recipient.Account.Nickname.Equals(rowName, StringComparison.OrdinalIgnoreCase)
                    ? recipient.Account.Id
                    : (LocatePlayer(rowName)?.Account.Id ?? recipient.Account.Id);
                var rankValue = (ClubRank)ArgInt(args, 2, 3);

                var memberRow = new ClubMemberDto2
                {
                    AccountId = memberAccountId,
                    Nickname = rowName,
                    Unk1 = ArgInt(args, 3, 0),
                    Unk2 = ArgInt(args, 4, 0),
                    ClanRank = rankValue,
                    Unk4 = ArgInt(args, 5, 0),
                    Unk5 = ArgInt(args, 6, 0),
                    JoinDate = "",
                    Unk7 = ArgInt(args, 7, 0),
                    LastLogin = "",
                    ServerId = ArgInt(args, 10, -1),
                    ChannelId = ArgInt(args, 11, -1),
                    RoomId = ArgInt(args, 12, -1),
                    Unk11 = ArgInt(args, 8, 0),
                    Unk12 = ArgInt(args, 9, 0)
                };

                await recipient.Session.SendAsync(new ClubStuffListAck2Message(new[] { memberRow }));

                Reply(plr,
                    $"Stuff2 -> {recipient.Account.Nickname} row={rowName} rank={rankValue} " +
                    $"u1={memberRow.Unk1} u2={memberRow.Unk2} u4={memberRow.Unk4} u5={memberRow.Unk5} u7={memberRow.Unk7} u11={memberRow.Unk11} u12={memberRow.Unk12} " +
                    $"srv/ch/room={memberRow.ServerId}/{memberRow.ChannelId}/{memberRow.RoomId}");
                Console.WriteLine(
                    $"[ClubStuffList2 CUSTOM] target={recipient.Account.Nickname} row={rowName} rank={rankValue} " +
                    $"u1={memberRow.Unk1} u2={memberRow.Unk2} u4={memberRow.Unk4} u5={memberRow.Unk5} u7={memberRow.Unk7} u11={memberRow.Unk11} u12={memberRow.Unk12} " +
                    $"srv/ch/room={memberRow.ServerId}/{memberRow.ChannelId}/{memberRow.RoomId}");
                return true;
            }

            public string Help()
            {
                return "stuff2 <targetNick> <rowNick> <clubRank> <unk1> <unk2> <unk4> <unk5> <unk7> <unk11> <unk12> [server] [channel] [room]";
            }
        }

        private class ImportuneAckSub : ICommand
        {
            public ImportuneAckSub()
            {
                Name = "importune_ack";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 1)
                {
                    Reply(plr, $"Uso: custom importune_ack <successValue> [failValue]");
                    Reply(plr, $"Actual: success={GeneralService.NoteImportuneItemAckSuccessValue} fail={GeneralService.NoteImportuneItemAckFailValue}");
                    return true;
                }

                if (!int.TryParse(args[0], out var successCode))
                {
                    Reply(plr, "successValue invalido.");
                    return true;
                }

                var failCode = GeneralService.NoteImportuneItemAckFailValue;
                if (args.Length > 1 && !int.TryParse(args[1], out failCode))
                {
                    Reply(plr, "failValue invalido.");
                    return true;
                }

                GeneralService.NoteImportuneItemAckSuccessValue = successCode;
                GeneralService.NoteImportuneItemAckFailValue = failCode;

                Reply(plr, $"NoteImportuneItemAck values -> success={successCode} fail={failCode}");
                Console.WriteLine($"[IMPORTUNE ACK DEBUG] success={successCode} fail={failCode}");
                return true;
            }

            public string Help()
            {
                return "importune_ack <successValue> [failValue]";
            }
        }

        private class ImportuneReqSub : ICommand
        {
            public ImportuneReqSub()
            {
                Name = "importune_req";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = Array.Empty<ICommand>();
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 3)
                {
                    Reply(plr, "Uso player: custom importune_req <targetNick> <title> <body> [unk2] [unk5] [itemId] [period] [color] [effect]");
                    Reply(plr, "Uso consola: custom importune_req <senderNick> <targetNick> <title> <body> [unk2] [unk5] [itemId] [period] [color] [effect]");
                    return true;
                }

                Player sender = plr;
                int shift = 0;

                if (sender?.Session == null)
                {
                    if (args.Length < 4)
                    {
                        Reply(plr, "Con consola necesita senderNick y targetNick.");
                        return true;
                    }

                    sender = LocatePlayer(args[0]);
                    if (sender?.Session == null)
                    {
                        Reply(plr, $"Sender '{args[0]}' no esta ingame.");
                        return true;
                    }

                    shift = 1;
                }

                var targetNick = args[shift + 0].Replace('_', ' ');
                var titleText = args[shift + 1].Replace('_', ' ');
                var bodyText = args[shift + 2].Replace('_', ' ');
                var secondValue = ArgLong(args, shift + 3, 4);
                var fifthValue = ArgInt(args, shift + 4, 6);
                var itemNumberArg = ArgUInt(args, shift + 5, 2000080);
                var periodLen = ArgUShort(args, shift + 6, 1);
                var colorValue = ArgByte(args, shift + 7, 0);
                var effectValue = ArgUInt(args, shift + 8, 0);

                var importuneMessage = new NoteImportuneItemReqMessage
                {
                    Unk1 = targetNick,
                    Unk2 = secondValue,
                    Unk3 = titleText,
                    Unk4 = bodyText,
                    Unk5 = fifthValue,
                    Unk6 = new Network.Data.Game.ShopItemDto
                    {
                        ItemNumber = itemNumberArg,
                        PriceType = 0,
                        PeriodType = 0,
                        Period = periodLen,
                        Color = colorValue,
                        Effect = effectValue
                    }
                };

                await new GeneralService().NoteImportuneItemReq(sender.Session, importuneMessage);
                Reply(plr, $"Importune req enviado sender={sender.Account.Nickname} target={targetNick} title={titleText}");
                return true;
            }

            public string Help()
            {
                return "importune_req [senderNick] <targetNick> <title> <body> [unk2] [unk5] [itemId] [period] [color] [effect]";
            }
        }

        private static void Reply(Player plr, string msg)
        {
            if (plr == null)
                CommandManager.Logger.Information(msg);
            else
                plr.SendConsoleMessage(S4Color.Green + msg);
        }

        private static Player LocatePlayer(string lookupKey)
        {
            return GameServer.Instance.PlayerManager.FirstOrDefault(candidate =>
                candidate?.Account != null &&
                (
                    string.Equals(candidate.Account.Nickname, lookupKey, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Account.Username, lookupKey, StringComparison.OrdinalIgnoreCase) ||
                    (ulong.TryParse(lookupKey, out var numericId) && (ulong)candidate.Account.Id == numericId)
                ));
        }

        private static int ArgInt(string[] args, int index, int fallback)
        {
            return args.Length > index && int.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        private static long ArgLong(string[] args, int index, long fallback)
        {
            return args.Length > index && long.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        private static uint ArgUInt(string[] args, int index, uint fallback)
        {
            return args.Length > index && uint.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        private static ushort ArgUShort(string[] args, int index, ushort fallback)
        {
            return args.Length > index && ushort.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        private static byte ArgByte(string[] args, int index, byte fallback)
        {
            return args.Length > index && byte.TryParse(args[index], out var value)
                ? value
                : fallback;
        }

        private static string[] SplitCsv(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "-" || value == "_")
                return Array.Empty<string>();

            return value
                .Trim()
                .Trim('[', ']')
                .Trim('{', '}')
                .Replace("'", string.Empty)
                .Replace("\"", string.Empty)
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(token => token.Trim().Replace('_', ' '))
                .ToArray();
        }
    }
}

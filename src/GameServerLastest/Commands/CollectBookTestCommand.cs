using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Services;
namespace Santana.Commands
{
    internal class CollectBookTestCommand : ICommand
    {
        public CollectBookTestCommand()
        {
            Name = "/cbtest";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = Array.Empty<ICommand>();
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length == 0)
            {
                Notify(plr, Help());
                return true;
            }
            try
            {
                if (plr == null)
                {
                    if (args.Length < 2)
                    {
                        Notify(null, Help());
                        return true;
                    }
                    var wanted = args[0];
                    plr = GameServer.Instance.PlayerManager.FirstOrDefault(x =>
                        x?.Account != null &&
                        (string.Equals(x.Account.Nickname, wanted, StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Account.Username, wanted, StringComparison.OrdinalIgnoreCase) ||
                         (ulong.TryParse(wanted, out var wantedId) && (ulong)x.Account.Id == wantedId)));
                    if (plr == null)
                    {
                        Notify(null, $"cbtest target not online: {wanted}");
                        return true;
                    }
                    args = args.Skip(1).ToArray();
                }
                var command = args[0].Trim().ToLowerInvariant();
                if (command == "updatereq")
                {
                    await plr.SendAsync(new CollectBook_UpdateRequest_Ack());
                    Notify(plr, "cbtest updatereq sent");
                    return true;
                }
                if (command == "updatecheck")
                {
                    var checkData = args.Length >= 2 && args[1] != "-" ? args[1] : null;
                    await plr.SendAsync(new CollectBook_UpdateCheck_Ack { Data = checkData });
                    Notify(plr, $"cbtest updatecheck sent data='{checkData ?? "<null>"}'");
                    return true;
                }
                if (command == "updateinfo")
                {
                    var a1 = ArgInt(args, 1, 0);
                    var a2 = ArgInt(args, 2, 0);
                    var a3 = ArgInt(args, 3, 0);
                    var tail = args.Length >= 5 ? string.Join(" ", args.Skip(4)) : string.Empty;
                    await plr.SendAsync(new CollectBook_UpdateInfo_Ack
                    {
                        Unk1 = Array.Empty<byte>(),
                        Unk2 = a2,
                        Unk3 = a3,
                        Unk4 = tail
                    });
                    Notify(plr, "cbtest updateinfo sent");
                    return true;
                }
                if (command == "progress")
                {
                    var inventoryAck = ShopService.CreateCollectBookInventoryInfoAck(plr);
                    await plr.SendAsync(inventoryAck);
                    var slotSum = inventoryAck.Items?.Sum(x => x.Unk4 + x.Unk5 + x.Unk6 + x.Unk7 + x.Unk8 + x.Unk9) ?? 0;
                    Notify(plr, $"cbtest progress sent books={inventoryAck.Items?.Length ?? 0} filled={slotSum}");
                    return true;
                }
                if (command == "replay" || command == "rebuildsafe")
                {
                    var inventoryAck = ShopService.CreateCollectBookInventoryInfoAck(plr, true);
                    var stamp = args.Length >= 2 && args[1] == "now"
                        ? DateTime.UtcNow.ToString("yyyyMMddHHmmss")
                        : ShopService.GetCollectBookVersion();
                    if (command == "rebuildsafe")
                    {
                        await plr.SendAsync(new CollectBook_UpdateRequest_Ack());
                        await plr.SendAsync(new CollectBook_UpdateCheck_Ack { Data = stamp });
                    }
                    foreach (var book in inventoryAck.Items ?? Array.Empty<CollectBook_ItemRegist_Ack>())
                        await plr.SendAsync(book);
                    await plr.SendAsync(inventoryAck);
                    var slotSum = inventoryAck.Items?.Sum(x => x.Unk4 + x.Unk5 + x.Unk6 + x.Unk7 + x.Unk8 + x.Unk9) ?? 0;
                    Notify(plr, $"cbtest {command} sent books={inventoryAck.Items?.Length ?? 0} filled={slotSum} version='{stamp}'");
                    return true;
                }
                if (command == "effectinv" || command == "effect")
                {
                    var amount = ArgInt(args, 1, 0);
                    var effectItem = AssembleEffectItem(args.Skip(command == "effect" ? 2 : 1).ToArray());
                    if (command == "effect")
                    {
                        await plr.SendAsync(new CollectBook_EffectRegist_Ack
                        {
                            Value = amount,
                            Items = new[] { effectItem }
                        });
                    }
                    else
                    {
                        await plr.SendAsync(new CollectBookInvenEffectInfoAckMessage
                        {
                            Unk = 1,
                            active = effectItem.Unk1,
                            Unk3 = effectItem.Unk3,
                            Unk4 = effectItem.Unk4,
                            nametagid = (uint)Math.Max(effectItem.Unk5, 0),
                            Unk5 = effectItem.Unk6,
                            Unk6 = 0,
                            days = effectItem.Unk7,
                            nametag = effectItem.Unk8,
                            zero = effectItem.Unk9,
                            zero2 = effectItem.Unk10,
                            zero3 = effectItem.Unk11
                        });
                    }
                    Notify(plr, $"cbtest {command} sent");
                    return true;
                }
                if (command == "expire")
                {
                    var amount = ArgInt(args, 1, 0);
                    await plr.SendAsync(new CollectBook_ExpireBookReward_Ack { Unk = amount });
                    Notify(plr, $"cbtest expire sent {amount}");
                    return true;
                }
                if (command == "reuse")
                {
                    var amount = ArgUInt(args, 1, 0);
                    await plr.SendAsync(new CollectBook_ResuseBookReward_Ack { Value = amount });
                    Notify(plr, $"cbtest reuse sent {amount}");
                    return true;
                }
                if (command == "unreg")
                {
                    var amount = ArgInt(args, 1, 0);
                    await plr.SendAsync(new CollectBook_BookUnRegist_Ack { Value = amount });
                    Notify(plr, $"cbtest unreg sent {amount}");
                    return true;
                }
                if (command == "bookreward")
                {
                    var amount = ArgInt(args, 1, 0);
                    await plr.SendAsync(new CollectBook_BookUseReward_Ack
                    {
                        Value = amount,
                        Data = new BookUseRewardData
                        {
                            Unk1 = ArgInt(args, 2, 0),
                            Unk2 = ArgInt(args, 3, 0),
                            Unk3 = ArgInt(args, 4, 0),
                            Unk4 = ArgInt(args, 5, 0),
                            Unk5 = ArgString(args, 6),
                            Unk6 = ArgString(args, 7),
                            Unk7 = ArgString(args, 8)
                        }
                    });
                    Notify(plr, $"cbtest bookreward sent {amount}");
                    return true;
                }
                if ((command != "ack" && command != "inv" && command != "both") || args.Length < 2 || !ulong.TryParse(args[1], out var key))
                {
                    Notify(plr, Help());
                    return true;
                }
                var count = args.Length >= 3 && int.TryParse(args[2], out var parsedCount) ? parsedCount : 1;
                var extra = args.Length >= 4 && int.TryParse(args[3], out var parsedExtra) ? parsedExtra : 0;
                var flagValues = DecodeFlagString(args.Length >= 5 ? args[4] : "111111");
                var registAck = new CollectBook_ItemRegist_Ack
                {
                    Unk1 = key,
                    Unk2 = count,
                    Unk3 = extra,
                    Unk4 = flagValues[0],
                    Unk5 = flagValues[1],
                    Unk6 = flagValues[2],
                    Unk7 = flagValues[3],
                    Unk8 = flagValues[4],
                    Unk9 = flagValues[5]
                };
                if (command == "ack" || command == "both")
                {
                    await plr.SendAsync(registAck);
                    if (command == "ack")
                    {
                        Notify(plr, $"cbtest ack sent book={key}");
                        return true;
                    }
                }
                Notify(plr, $"cbtest inv sent book={key}");
            }
            catch (Exception ex)
            {
                Notify(plr, "cbtest failed: " + ex.Message);
            }
            return true;
        }
        public string Help()
        {
            return "/cbtest updatereq | updatecheck [data|-] | updateinfo u1 u2 u3 text | progress | replay | rebuildsafe [now] | ack|inv|both <book> [u2] [u3] [flags] | effectinv/effect ... | expire/reuse/unreg/bookreward ...";
        }
        private static int[] DecodeFlagString(string raw)
        {
            var result = new int[6];
            raw = (raw ?? string.Empty).Trim();
            if (raw.Contains(","))
            {
                var pieces = raw.Split(',');
                for (var i = 0; i < result.Length && i < pieces.Length; i++)
                    result[i] = int.TryParse(pieces[i], out var value) ? value : 0;
                return result;
            }
            for (var i = 0; i < result.Length && i < raw.Length; i++)
                result[i] = raw[i] == '0' ? 0 : 1;
            return result;
        }
        private static CollectBookEffectItem AssembleEffectItem(string[] args)
        {
            return new CollectBookEffectItem
            {
                Unk1 = (byte)ArgInt(args, 0, 0),
                Unk2 = ArgInt(args, 1, 0),
                Unk3 = (short)ArgInt(args, 2, 0),
                Unk4 = ArgInt(args, 3, 0),
                Unk5 = ArgInt(args, 4, 0),
                Unk6 = ArgInt(args, 5, 0),
                Unk7 = ArgString(args, 6),
                Unk8 = ArgString(args, 7),
                Unk9 = ArgString(args, 8),
                Unk10 = ArgString(args, 9),
                Unk11 = ArgString(args, 10)
            };
        }
        private static int ArgInt(string[] args, int index, int fallback)
        {
            return args.Length > index && int.TryParse(args[index], out var value)
                ? value
                : fallback;
        }
        private static uint ArgUInt(string[] args, int index, uint fallback)
        {
            return args.Length > index && uint.TryParse(args[index], out var value)
                ? value
                : fallback;
        }
        private static string ArgString(string[] args, int index)
        {
            return args.Length > index && args[index] != "-"
                ? args[index]
                : string.Empty;
        }
        private static void Notify(Player plr, string msg)
        {
            if (plr == null)
                { }
            else
                plr.SendConsoleMessage(S4Color.Green + msg);
        }
    }
}

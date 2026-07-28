using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Game;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Services;
using ExpressMapper.Extensions;

namespace Santana.Commands
{
    internal class CombiTestCommand : ICommand
    {
        public CombiTestCommand()
        {
            Name = "/combitest";
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
            try
            {
                if (plr == null)
                {
                    if (args.Length < 2)
                    {
                        Reply(null, Help());
                        return true;
                    }

                    plr = Lookup(args[0]);
                    if (plr == null)
                    {
                        Reply(null, $"combitest target not online: {args[0]}");
                        return true;
                    }

                    args = args.Skip(1).ToArray();
                }

                if (args.Length == 0)
                {
                    Reply(plr, Help());
                    return true;
                }

                var verb = args[0].Trim().ToLowerInvariant();

                if (verb == "live")
                {
                    await CommunityService.SendCombiList(plr);
                    Reply(plr, "combitest live sent");
                    Console.WriteLine($"[CombiTest] live CombiListAck sent: player={plr.Account.Nickname}");
                    return true;
                }

                if (verb == "account")
                {
                    if (args.Length < 6 ||
                        !uint.TryParse(args[1], out var val6) ||
                        !uint.TryParse(args[2], out var val7) ||
                        !uint.TryParse(args[3], out var val8) ||
                        !uint.TryParse(args[4], out var val9) ||
                        !uint.TryParse(args[5], out var val10))
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    var payload = plr.Map<Player, PlayerAccountInfoDto>();
                    payload.Unk6 = val6;
                    payload.Unk7 = val7;
                    payload.Unk8 = val8;
                    payload.Unk9 = val9;
                    payload.Unk10 = val10;

                    await plr.SendAsync(new PlayerAccountInfoAckMessage(payload));
                    Console.WriteLine($"[CombiTest] account sent: player={plr.Account.Nickname}, unk6={val6},unk7={val7},unk8={val8},unk9={val9},unk10={val10}");
                    Reply(plr, $"combitest account sent {val6},{val7},{val8},{val9},{val10}");
                    return true;
                }

                if (verb == "accountfield")
                {
                    if (args.Length < 3 || !uint.TryParse(args[2], out var amount))
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    var which = args[1].Trim().ToLowerInvariant();
                    var payload = plr.Map<Player, PlayerAccountInfoDto>();

                    switch (which)
                    {
                        case "totalexp":
                            payload.TotalExp = (int)Math.Min(amount, int.MaxValue);
                            break;
                        case "combimasterexp":
                        case "masterexp":
                            payload.CombiMasterExp = amount;
                            break;
                        case "unk1":
                            payload.Unk1 = amount;
                            break;
                        case "unk2":
                            payload.Unk2 = amount;
                            break;
                        case "unk3":
                            payload.Unk3 = amount;
                            break;
                        case "level":
                            payload.Level = (byte)Math.Min(amount, byte.MaxValue);
                            break;
                        case "unk4":
                            payload.Unk4 = (byte)Math.Min(amount, byte.MaxValue);
                            break;
                        case "unk5":
                            payload.Unk5 = amount;
                            break;
                        case "unk6":
                            payload.Unk6 = amount;
                            break;
                        case "unk7":
                            payload.Unk7 = amount;
                            break;
                        case "unk8":
                            payload.Unk8 = amount;
                            break;
                        case "unk9":
                            payload.Unk9 = amount;
                            break;
                        case "unk10":
                            payload.Unk10 = amount;
                            break;
                        default:
                            Reply(plr, Help());
                            return true;
                    }

                    await plr.SendAsync(new PlayerAccountInfoAckMessage(payload));
                    Console.WriteLine($"[CombiTest] accountfield sent: player={plr.Account.Nickname}, field={which}, value={amount}");
                    Reply(plr, $"combitest accountfield sent {which}={amount}");
                    return true;
                }

                if (verb == "online")
                {
                    if (args.Length < 2)
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    var mate = Lookup(args[1]);
                    if (mate == null)
                    {
                        Reply(plr, $"combitest online target not online: {args[1]}");
                        return true;
                    }

                    var row = new CombiDto
                    {
                        Unk1 = (ulong)mate.Account.Id,
                        Unk2 = 2,
                        Unk3 = 2,
                        Unk4 = 60,
                        Unk5 = 200,
                        Unk6 = (ulong)mate.Account.Id,
                        Unk7 = 2250,
                        Unk8 = 1125,
                        Unk9 = 1150,
                        Unk10 = "CampoCombiNose",
                        Unk11 = mate.Account.Nickname,
                        Unk12 = "ASAS",
                        Unk13 = DateTime.Now.ToString("yyyyMMddHHmmss")
                    };

                    await PushPresence(plr, row, "COMBITEST.ONLINE.BEFORE");
                    await SendChat(plr, new CombiListAckMessage(new[] { row }));
                    await PushPresence(plr, row, "COMBITEST.ONLINE.AFTER");
                    Trace("online", plr, row);
                    Reply(plr, $"combitest online sent target={mate.Account.Nickname}({mate.Account.Id})");
                    return true;
                }

                if (verb == "one" || verb == "list")
                {
                    var row = BuildRow(args.Skip(1).ToArray(), plr);
                    if (row == null)
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    await PushPresence(plr, row, "COMBITEST.LIST.BEFORE");
                    await SendChat(plr, new CombiListAckMessage(new[] { row }));
                    await PushPresence(plr, row, "COMBITEST.LIST.AFTER");
                    Trace("list", plr, row);
                    Reply(plr, $"combitest list sent row={row.Unk1}");
                    return true;
                }

                if (verb == "action")
                {
                    if (args.Length < 3 || !int.TryParse(args[1], out var outcome) || !int.TryParse(args[2], out var verbCode))
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    var row = BuildRow(args.Skip(3).ToArray(), plr);
                    if (row == null)
                    {
                        Reply(plr, Help());
                        return true;
                    }

                    await SendChat(plr, new CombiActionAckMessage(outcome, verbCode, row));
                    await PushPresence(plr, row, "COMBITEST.ACTION");
                    Trace($"action result={outcome} action={verbCode}", plr, row);
                    Reply(plr, $"combitest action sent row={row.Unk1}");
                    return true;
                }

                Reply(plr, Help());
            }
            catch (Exception ex)
            {
                Console.WriteLine("[CombiTest] failed: " + ex);
                Reply(plr, "combitest failed: " + ex.Message);
            }

            return true;
        }

        public string Help()
        {
            return "/combitest live | /combitest online <target> | /combitest account <unk6> <unk7> <unk8> <unk9> <unk10> | /combitest accountfield <totalexp|combimasterexp|level|unk1..unk10> <value> | /combitest list <u1> <u2> <u3> <u4> <u5> <u6> <u7> <u8> <u9> <u10> <u11> <u12> <u13> | /combitest action <result> <action> <same 13 fields> | console: /combitest <player> ...";
        }

        private static CombiDto BuildRow(string[] parts, Player plr)
        {
            if (parts.Length < 9)
                return null;

            if (!ulong.TryParse(parts[0], out var f1) ||
                !uint.TryParse(parts[1], out var f2) ||
                !uint.TryParse(parts[2], out var f3) ||
                !uint.TryParse(parts[3], out var f4) ||
                !ulong.TryParse(parts[4], out var f5) ||
                !ulong.TryParse(parts[5], out var f6) ||
                !ulong.TryParse(parts[6], out var f7) ||
                !ulong.TryParse(parts[7], out var f8) ||
                !ulong.TryParse(parts[8], out var f9))
                return null;

            return new CombiDto
            {
                Unk1 = f1,
                Unk2 = f2,
                Unk3 = f3,
                Unk4 = f4,
                Unk5 = f5,
                Unk6 = f6,
                Unk7 = f7,
                Unk8 = f8,
                Unk9 = f9,
                Unk10 = parts.Length > 9 ? parts[9] : "CampoCombiNose",
                Unk11 = parts.Length > 10 ? parts[10] : plr.Account.Nickname,
                Unk12 = parts.Length > 11 ? parts[11] : "test",
                Unk13 = parts.Length > 12 ? parts[12] : DateTime.Now.ToString("yyyyMMddHHmmss")
            };
        }

        private static Player Lookup(string token)
        {
            return GameServer.Instance.PlayerManager.FirstOrDefault(candidate =>
                candidate?.Account != null &&
                (string.Equals(candidate.Account.Nickname, token, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(candidate.Account.Username, token, StringComparison.OrdinalIgnoreCase) ||
                 (ulong.TryParse(token, out var wantedId) && (ulong)candidate.Account.Id == wantedId)));
        }

        private static Task SendChat(Player plr, object message)
        {
            return plr.ChatSession != null ? plr.ChatSession.SendAsync(message) : Task.CompletedTask;
        }

        private static async Task PushPresence(Player viewer, CombiDto row, string origin)
        {
            if (viewer?.ChatSession == null || row?.Unk6 == 0)
                return;

            var mate = GameServer.Instance.PlayerManager.Get(row.Unk6);
            if (mate == null)
            {
                Console.WriteLine($"[CombiTest] {origin} target offline/not found: viewer={viewer.Account.Id}, target={row.Unk6}");
                return;
            }

            await viewer.ChatSession.SendAsync(new ChatPlayerInfoListAckMessage(new[] { mate.Map<Player, PlayerInfoDto>() }));
            await viewer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(mate.Map<Player, PlayerInfoDto>()));
            Club.SendLivePresence(viewer, mate, origin);
            Console.WriteLine($"[CombiTest] {origin} presence sent: viewer={viewer.Account.Id}, target={mate.Account.Id}");
        }

        private static void Trace(string label, Player plr, CombiDto row)
        {
            Console.WriteLine(
                $"[CombiTest] {label} sent: player={plr.Account.Nickname}, " +
                $"u1={row.Unk1},u2={row.Unk2},u3={row.Unk3},u4={row.Unk4},u5={row.Unk5},u6={row.Unk6},u7={row.Unk7},u8={row.Unk8},u9={row.Unk9}," +
                $"u10='{row.Unk10}',u11='{row.Unk11}',u12='{row.Unk12}',u13='{row.Unk13}'");
        }

        private static void Reply(Player plr, string message)
        {
            if (plr == null)
                Console.WriteLine(message);
            else
                plr.SendConsoleMessage(S4Color.Green + message);
        }
    }
}

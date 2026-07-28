using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ExpressMapper.Extensions;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Club;
using Santana.Network.Message.Chat;
using Santana.Network.Services;

namespace Santana.Commands
{
    internal class OnlineCommand : ICommand
    {
        public OnlineCommand()
        {
            Name = "/online";
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
            await PresenceProbe.BroadcastForcedState(server, plr, 2, "ONLINE", false);
            return true;
        }

        public string Help()
        {
            return "/online";
        }
    }

    internal class OfflineCommand : ICommand
    {
        public OfflineCommand()
        {
            Name = "/offline";
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
            await PresenceProbe.BroadcastForcedState(server, plr, 0, "OFFLINE", true);
            return true;
        }

        public string Help()
        {
            return "/offline";
        }
    }

    internal class LoginStateCommand : ICommand
    {
        public LoginStateCommand()
        {
            Name = "/loginstate";
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
            if (args.Length < 1 || !int.TryParse(args[0], out var stateValue))
            {
                PresenceProbe.Announce(plr, "Uso: /loginstate <state> [targetId|all] [viewerId|all]");
                return true;
            }

            var subjects = PresenceProbe.PickTargets(server, args, 1);
            var observers = PresenceProbe.PickViewers(server, args, 2);
            var pushed = 0;

            foreach (var observer in observers)
            foreach (var subject in subjects)
            {
                await observer.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(stateValue, subject.Account.Id));
                pushed++;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /loginstate state={stateValue} targets={subjects.Length} viewers={observers.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/loginstate <state> [targetId|all] [viewerId|all]";
    }

    internal class ClubMemberDebugCommand : ICommand
    {
        public ClubMemberDebugCommand()
        {
            Name = "/clubmember";
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
            var observers = PresenceProbe.PickViewers(server, args, 0);
            var pushed = 0;

            foreach (var observer in observers)
            {
                pushed += await PresenceProbe.PushClubSnapshot(server, observer);
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /clubmember viewers={observers.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/clubmember [viewerId|all]";
    }

    internal class PlayerInfoDebugCommand : ICommand
    {
        public PlayerInfoDebugCommand()
        {
            Name = "/playerinfo";
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
            var subjects = PresenceProbe.PickTargets(server, args, 0);
            var observers = PresenceProbe.PickViewers(server, args, 1);
            var wipeLocation = args.Length > 2 && args[2].Equals("offline", StringComparison.OrdinalIgnoreCase);
            var pushed = 0;

            foreach (var observer in observers)
            foreach (var subject in subjects)
            {
                var dto = subject.Map<Player, PlayerInfoDto>();
                if (wipeLocation)
                    dto.Location = new PlayerLocationDto();

                await observer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(dto));
                pushed++;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /playerinfo mode={(wipeLocation ? "offline" : "live")} targets={subjects.Length} viewers={observers.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/playerinfo [targetId|all] [viewerId|all] [live|offline]";
    }

    internal class PlayerPositionDebugCommand : ICommand
    {
        public PlayerPositionDebugCommand()
        {
            Name = "/position";
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
            var subjects = PresenceProbe.PickTargets(server, args, 0);
            var observers = PresenceProbe.PickViewers(server, args, 1);
            var wipeLocation = args.Length > 2 && args[2].Equals("offline", StringComparison.OrdinalIgnoreCase);
            var pushed = 0;

            foreach (var observer in observers)
            foreach (var subject in subjects)
            {
                var where = wipeLocation ? new PlayerLocationDto() : subject.Map<Player, PlayerLocationDto>();
                await observer.ChatSession.SendAsync(new PlayerPositionAckMessage(subject.Account.Id, where));
                pushed++;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /position mode={(wipeLocation ? "offline" : "live")} targets={subjects.Length} viewers={observers.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/position [targetId|all] [viewerId|all] [live|offline]";
    }

    internal class PlayerInfoListDebugCommand : ICommand
    {
        public PlayerInfoListDebugCommand()
        {
            Name = "/infolist";
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
            var observers = PresenceProbe.PickViewers(server, args, 0);
            var roster = PresenceProbe.ActivePlayers(server);
            var pushed = 0;

            foreach (var observer in observers)
            {
                await PresenceProbe.PushInfoList(server, observer, roster);
                pushed++;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /infolist viewers={observers.Length} players={roster.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/infolist [viewerId|all]";
    }

    internal class CombiListDebugCommand : ICommand
    {
        public CombiListDebugCommand()
        {
            Name = "/combilist";
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
            var observers = PresenceProbe.PickViewers(server, args, 0);
            var pushed = 0;

            foreach (var observer in observers)
            {
                await CommunityService.SendCombiList(observer);
                pushed++;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /combilist viewers={observers.Length} packets={pushed}");
            return true;
        }

        public string Help() => "/combilist [viewerId|all]";
    }

    internal class PlayerLocationDebugCommand : ICommand
    {
        public PlayerLocationDebugCommand()
        {
            Name = "/locinfo";
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
            if (args.Length < 8 ||
                !int.TryParse(args[2], out var serverGroup) ||
                !int.TryParse(args[3], out var channel) ||
                !int.TryParse(args[4], out var roomSlot) ||
                !int.TryParse(args[5], out var spare) ||
                !int.TryParse(args[6], out var gameSrv) ||
                !int.TryParse(args[7], out var chatSrv))
            {
                PresenceProbe.Announce(plr, "Uso: /locinfo <targetId|all> <viewerId|all> <sg> <ch> <room> <unk> <game> <chat>");
                return true;
            }

            var subjects = PresenceProbe.PickTargets(server, args, 0);
            var observers = PresenceProbe.PickViewers(server, args, 1);
            var pushed = 0;

            foreach (var observer in observers)
            foreach (var subject in subjects)
            {
                var dto = subject.Map<Player, PlayerInfoDto>();
                dto.Location = new PlayerLocationDto
                {
                    ServerGroupId = serverGroup,
                    ChannelId = channel,
                    RoomId = roomSlot,
                    Unk = spare,
                    GameServerId = gameSrv,
                    ChatServerId = chatSrv
                };

                await observer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(dto));
                await observer.ChatSession.SendAsync(new PlayerPositionAckMessage(subject.Account.Id, dto.Location));
                pushed += 2;
            }

            PresenceProbe.Announce(plr, $"[PRESENCE TEST] /locinfo targets={subjects.Length} viewers={observers.Length} loc={serverGroup},{channel},{roomSlot},{spare},{gameSrv},{chatSrv} packets={pushed}");
            return true;
        }

        public string Help() => "/locinfo <targetId|all> <viewerId|all> <sg> <ch> <room> <unk> <game> <chat>";
    }

    internal static class PresenceProbe
    {
        public static async Task BroadcastForcedState(GameServer server, Player caller, int state, string label, bool forceOfflineLocation)
        {
            var everyone = server.PlayerManager
                .Where(x => x?.Account != null)
                .ToArray();

            var observers = everyone
                .Where(x => x.ChatSession != null)
                .ToArray();

            var pushed = 0;
            foreach (var observer in observers)
            {
                if (state == 2)
                {
                    pushed += await PushOnlineRefresh(server, observer);
                    continue;
                }

                foreach (var subject in everyone)
                {
                    await observer.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(state, subject.Account.Id));
                    pushed++;

                    var dto = subject.Map<Player, PlayerInfoDto>();
                    if (forceOfflineLocation)
                    {
                        dto.Location = new PlayerLocationDto();
                    }

                    await observer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(dto));
                    await observer.ChatSession.SendAsync(new PlayerPositionAckMessage(subject.Account.Id, dto.Location));
                    pushed += 2;
                }
            }

            var line = $"[PRESENCE DEBUG] FORCED {label}: targets={everyone.Length} viewers={observers.Length} packets={pushed}";
            Console.WriteLine(line);
            caller?.SendConsoleMessage(S4Color.Green + line);
        }

        private static async Task<int> PushOnlineRefresh(GameServer server, Player observer)
        {
            if (observer?.ChatSession == null || observer.Club == null)
                return 0;

            var pushed = 0;
            await PushClubSnapshot(server, observer);
            pushed++;

            foreach (var member in observer.Club.Players.Values)
            {
                var live = server.PlayerManager
                    .FirstOrDefault(p => p?.Account != null && p.Account.Id == member.AccountId);

                if (live == null)
                    continue;

                await observer.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(2, member.AccountId));
                await observer.SendAsync(new ClubMemberLoginStateAckMessage(2, member.AccountId));
                pushed += 2;
            }

            await CommunityService.SendCombiList(observer);
            pushed++;
            return pushed;
        }

        public static Player[] ActivePlayers(GameServer server)
        {
            return server.PlayerManager
                .Where(x => x?.Account != null)
                .ToArray();
        }

        public static Player[] PickTargets(GameServer server, string[] args, int index)
        {
            return SelectPlayers(server, args, index, requireChat: false);
        }

        public static Player[] PickViewers(GameServer server, string[] args, int index)
        {
            return SelectPlayers(server, args, index, requireChat: true);
        }

        public static void Announce(Player plr, string message)
        {
            Console.WriteLine(message);
            plr?.SendConsoleMessage(S4Color.Green + message);
        }

        private static Player[] SelectPlayers(GameServer server, string[] args, int index, bool requireChat)
        {
            var pool = ActivePlayers(server);
            if (requireChat)
                pool = pool.Where(x => x.ChatSession != null).ToArray();

            if (args.Length <= index || args[index].Equals("all", StringComparison.OrdinalIgnoreCase))
                return pool;

            if (!ulong.TryParse(args[index], out var wantedId))
                return Array.Empty<Player>();

            return pool
                .Where(x => x.Account.Id == wantedId)
                .ToArray();
        }

        public static async Task PushInfoList(GameServer server, Player observer, Player[] roster)
        {
            if (observer?.ChatSession == null)
                return;

            var dtos = roster
                .Where(x => x?.Account != null)
                .Select(x => x.Map<Player, PlayerInfoDto>())
                .ToArray();

            await observer.ChatSession.SendAsync(new ChatPlayerInfoListAckMessage(dtos));
        }

        public static async Task<int> PushClubSnapshot(GameServer server, Player observer)
        {
            if (observer?.ChatSession == null || observer.Club == null)
                return 0;

            var roster = new List<ClubMemberDto>();
            foreach (var member in observer.Club.Players.Values)
            {
                var live = server.PlayerManager
                    .FirstOrDefault(p => p?.Account != null && p.Account.Id == member.AccountId);

                if (live != null)
                    roster.Add(live.Map<Player, ClubMemberDto>());
                else if (member.Account != null)
                    roster.Add(member.Map<ClubPlayerInfo, ClubMemberDto>());
            }

            await observer.ChatSession.SendAsync(new ClubMemberListAckMessage(roster.ToArray()));
            return 1;
        }
    }
}

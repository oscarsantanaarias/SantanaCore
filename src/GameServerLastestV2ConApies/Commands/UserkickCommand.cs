using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SantanaLib.Threading.Tasks;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Network;
using Santana.Network.Message.Game;
namespace Santana.Commands
{
    internal class AllkickCommand : ICommand
    {
        public AllkickCommand()
        {
            Name = "/allkick";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > /allkick room | /allkick server", NewChatType.All);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /allkick room");
                plr.SendConsoleMessage(S4Color.Red + "> /allkick server");
                return true;
            }
            switch (args[0])
            {
                case "room":
                    {
                        if (plr?.Room != null)
                        {
                            var kicked = plr.Room.Players.Count() - 1;
                            foreach (var occupant in plr.Room.Players.Values)
                            {
                                if (occupant == plr)
                                    continue;
                                plr.Room.Leave(occupant);
                            }
                            plr.SendConsoleMessage($"\"{kicked}\"players have been kicked forcefully out of room");
                        }
                        else
                        {
                            plr.SendConsoleMessage(S4Color.Red + "You are not in a room");
                        }
                        break;
                    }
                case "server":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                        {
                            plr.SendConsoleMessage(S4Color.Red + "You don't have a right");
                            return true;
                        }
                        plr.SendConsoleMessage($"Please wait..");
                        foreach (var conn in GameServer.Instance.Sessions.Values.Cast<GameSession>())
                        {
                            if (conn.Player == plr)
                                continue;
                            conn.Player?.Room?.Leave(conn.Player);
                        }
                        await Task.Delay(1000);
                        foreach (var conn in GameServer.Instance.Sessions.Values.Cast<GameSession>())
                        {
                            if (conn.Player == plr)
                                continue;
                            await conn.SendAsync(new ItemUseChangeNickAckMessage { Result = 0 });
                            await conn.SendAsync(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
                        }
                        await Task.Delay(1000);
                        var kicked = 0;
                        foreach (var conn in GameServer.Instance.Sessions.Values.Cast<GameSession>())
                        {
                            if (conn.Player == plr)
                                continue;
                            kicked++;
                            await conn.CloseAsync();
                        }
                        plr.SendConsoleMessage($"\"{kicked}\"players have been kicked forcefully");
                        break;
                    }
            }
            return true;
        }
        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("");
                sb.AppendLine(cmd.Help());
            }
            return sb.ToString();
        }
    }
    internal class RoomkickCommand : ICommand
    {
        public RoomkickCommand()
        {
            Name = "/roomkick";
            AllowConsole = true;
            Permission = SecurityLevel.GameSage;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > /roomkick <username>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /roomkick <username>");
                return true;
            }
            var name = args[0];
            var victim = GameServer.Instance.PlayerManager.Get(name);
            if (victim?.Room == null)
            {
                if (victim == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Unknown Player");
                    plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                }
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", "Player is not in a Room", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Player is not in a Room");
                }
                return true;
            }
            victim.Room.Leave(victim, RoomLeaveReason.ModeratorKick);
            plr?.Channel?.SendMessage(plr, "system", "Player " + victim.Account.Nickname + " has been kicked forcefully out of room", NewChatType.Whisper);
            plr.SendConsoleMessage(S4Color.Green +
                             $"Player {victim.Account.Nickname} has been kicked forcefully out of room");
            return true;
        }
        public string Help()
        {
            return new UserkickCommand().Help();
        }
    }
    internal class UserkickCommand : ICommand
    {
        public UserkickCommand()
        {
            Name = "/userkick";
            AllowConsole = true;
            Permission = SecurityLevel.GameSage;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > /userkick <username>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /userkick <username>");
                return true;
            }
            var name = args[0];
            using (var handle = AuthDatabase.Open())
            {
                var record = (await DbUtil.FindAsync<AccountDto>(handle, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = name }))
                    ).FirstOrDefault();
                if (record == null)
                {
                    if (plr == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Unknown Player");
                        plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                    }
                    else
                    {
                        plr?.Channel?.SendMessage(plr, "system", "Player is not in a Room", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Red + "Player is not in a Room");
                    }
                    return true;
                }
                var victim = GameServer.Instance.PlayerManager.Get((ulong)record.Id);
                if (victim == null)
                {
                    if (plr != null)
                        plr.SendConsoleMessage(S4Color.Red + "Player is not online");
                    else
                        CommandManager.Logger.Information("That account exists but has no session open right now");
                    return true;
                }
                victim.Disconnect();
                if (plr != null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Player " + record.Nickname + "has been kicked forcefully", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + $"Player {record.Nickname} has been kicked forcefully");
                }
                else
                    CommandManager.Logger.Information("Session dropped; nobody in-game to notify since this came from the console");
            }
            return true;
        }
        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("");
                sb.AppendLine(cmd.Help());
            }
            return sb.ToString();
        }
    }
    internal class KickCommand : ICommand
    {
        public KickCommand()
        {
            Name = "/kick";
            AllowConsole = true;
            Permission = SecurityLevel.GameSage;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > /kick <username>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /kick <username>");
                return true;
            }
            var name = args[0];
            using (var handle = AuthDatabase.Open())
            {
                var record = (await DbUtil.FindAsync<AccountDto>(handle, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = name }))
                    ).FirstOrDefault();
                if (record == null)
                {
                    if (plr != null)
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                    else
                        CommandManager.Logger.Information("No account is registered under that nickname");
                    return true;
                }
                var victim = GameServer.Instance.PlayerManager.Get((ulong)record.Id);
                if (victim == null)
                {
                    if (plr != null)
                        plr.SendConsoleMessage(S4Color.Red + "Player is not online");
                    else
                        CommandManager.Logger.Information("That account exists but has no session open right now");
                    return true;
                }
                victim.Disconnect();
                if (plr != null)
                    plr.SendConsoleMessage(S4Color.Green + $"Player {record.Nickname} has been kicked forcefully");
                else
                    CommandManager.Logger.Information("Session dropped; nobody in-game to notify since this came from the console");
            }
            return true;
        }
        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("");
                sb.AppendLine(cmd.Help());
            }
            return sb.ToString();
        }
    }
}

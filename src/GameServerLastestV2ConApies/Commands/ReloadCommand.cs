using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SantanaLib.Threading.Tasks;
using MySqlConnector;
using Santana.Network;
using Santana.Network.Data.Club;
using Santana.Network.Message.Club;
using Santana.Network.Message.Game;
using Santana.Network.Services;
using Santana.Resource;
namespace Santana.Commands
{
    internal class ReloadCommand : ICommand
    {
        public ReloadCommand()
        {
            Name = "reload";
            AllowConsole = true;
            Permission = SecurityLevel.Developer;
            SubCommands = new ICommand[]
            {
                new ShopCommand(), new RandomShopCommand(), new ReqBoxCommand(), new RoomMassKickCommand(), new ServerMassKickCommand(),
                new ClubCommand(), new ConfigCommand()
            };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
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
        private class ClubCommand : ICommand
        {
            public ClubCommand()
            {
                Name = "clubs";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var note = "Reloading clubs, server may lag for a short period of time...";
                if (plr == null)
                {
                    CommandManager.Logger.Information(note);
                }
                else
                {
                    plr.Channel?.SendMessage(plr, "system", note, NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                server.ResourceCache.Clear(ResourceCacheType.Clubs);
                server.ClubManager = new ClubManager(server.ResourceCache.GetClubs());
                await ClubService.Update(null, true);
                note = "Club reload completed";
                if (plr == null)
                {
                    CommandManager.Logger.Information(note);
                }
                else
                {
                    plr.Channel?.SendMessage(plr, "system", note, NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                var (clanCount, rankRows) = ClubService.BuildClubRankListFromDatabase();
                foreach (var row in rankRows)
                {
                }
                if (plr != null && clanCount > 0)
                {
                    await plr.SendAsync(new ClubRankListAckMessage(clanCount, rankRows));
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class ShopCommand : ICommand
        {
            public ShopCommand()
            {
                Name = "shop";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                await Task.Run(async () =>
                {
                    var note = "Reloading Shop, Server might lag for a short time period";
                    if (plr == null)
                        CommandManager.Logger.Information(note);
                    else
                    {
                        plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Green + note);
                    }
                    server.BroadcastNotice(note);
                    ShopService.BumpShopVersion();
                    await ShopService.ShopUpdateMsg(null, true);
                    CommandManager.Logger.Information($"[NewShop] told every client to refresh the shop, now on version '{ShopService.GetShopVersion()}'");
                    note = "Shop reload completed";
                    server.BroadcastNotice(note);
                    if (plr == null)
                        CommandManager.Logger.Information(note);
                    else
                    {
                        plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Green + note);
                    }
                });
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class RandomShopCommand : ICommand
        {
            public RandomShopCommand()
            {
                Name = "randomshop";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                await Task.Run(async () =>
                {
                    var note = "Reloading Randomshop";
                    if (plr == null)
                        CommandManager.Logger.Information(note);
                    else
                    {
                        plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Green + note);
                    }
                    server.BroadcastNotice(note);
                    server.ResourceCache.Clear(ResourceCacheType.RandomShop);
                    await ShopService.RandomShopUpdateMsg(null, true);
                    note = "RandomShop reload completed";
                    server.BroadcastNotice(note);
                    if (plr == null)
                        CommandManager.Logger.Information(note);
                    else
                    {
                        plr.SendConsoleMessage(S4Color.Green + note);
                        plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    }
                });
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class ConfigCommand : ICommand
        {
            public ConfigCommand()
            {
                Name = "config";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                Config.Load();
                var note = "config reload completed";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr.SendConsoleMessage(S4Color.Green + note);
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                }
                    return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class ReqBoxCommand : ICommand
        {
            public ReqBoxCommand()
            {
                Name = "reqs";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var note = "Trying to fix stuck request boxes..";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                GameServer.Instance.Broadcast(new ServerResultAckMessage(ServerResult.ServerError));
                note = "Done trying to fix stuck request boxes.";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class ServerMassKickCommand : ICommand
        {
            public ServerMassKickCommand()
            {
                Name = "playerlist";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var note = "Kicking all players..";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                foreach (var gameSession in GameServer.Instance.Sessions.Values.Cast<GameSession>())
                    gameSession.Player?.Room?.Leave(gameSession.Player);
                await Task.Delay(1000);
                GameServer.Instance.Broadcast(new ItemUseChangeNickAckMessage { Result = 0 });
                GameServer.Instance.Broadcast(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
                await Task.Delay(1000);
                foreach (var anySession in GameServer.Instance.Sessions.Values)
                    anySession?.CloseAsync();
                note = "Done with kickall";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class RoomMassKickCommand : ICommand
        {
            public RoomMassKickCommand()
            {
                Name = "rooms";
                AllowConsole = true;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var note = "Kicking all players..";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                foreach (var rawSession in GameServer.Instance.Sessions.Values)
                {
                    var gameSession = (GameSession)rawSession;
                    gameSession.Player?.Room?.Leave(gameSession.Player);
                }
                note = "Done kicking all players from all rooms.";
                if (plr == null)
                    CommandManager.Logger.Information(note);
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", $"{note}", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Green + note);
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
    }
}

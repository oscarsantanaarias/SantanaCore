using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Message.Game;

namespace Santana.Commands
{
    internal class ServerCommand : ICommand
    {
        public ServerCommand()
        {
            Name = "server";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { new StatusCommand(), new CoinCommand() };
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
            var text = new StringBuilder();
            text.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                text.Append("");
                text.AppendLine(sub.Help());
            }

            return text.ToString();
        }

        private class StatusCommand : ICommand
        {
            public StatusCommand()
            {
                Name = "status";
                AllowConsole = true;
                Permission = SecurityLevel.GameMaster;
                SubCommands = new ICommand[] { };
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var elapsed = DateTime.Now - Process.GetCurrentProcess().StartTime;
                var uptime = new StringBuilder();
                if (elapsed.Days > 0)
                    uptime.AppendFormat("{0} days ", elapsed.Days);
                if (elapsed.Hours > 0)
                    uptime.AppendFormat("{0} hours ", elapsed.Hours);
                if (elapsed.Minutes > 0)
                    uptime.AppendFormat("{0} minutes ", elapsed.Minutes);
                if (elapsed.Seconds > 0)
                    uptime.AppendFormat("{0} seconds ", elapsed.Seconds);

                var onlineCount = 0;
                foreach (var session in server.Sessions.Values)
                {
                    if (((GameSession)session).IsLoggedIn())
                        onlineCount++;
                }

                var message = $"Uptime: {uptime} " +
                              $"Online: {onlineCount} ";

                if (plr == null)
                    CommandManager.Logger.Information(message);
                else
                    plr.SendConsoleMessage(S4Color.Green + message);

                return true;
            }

            public string Help()
            {
                return Name;
            }
        }

        private class CoinCommand : ICommand
        {
            public CoinCommand()
            {
                Name = "coin";
                AllowConsole = true;
                Permission = SecurityLevel.GameMaster;
                SubCommands = new ICommand[] { };
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (plr.Room == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "You're not inside a room");
                    return true;
                }

               // var amount = new Random().Next(100, 50000);
              //  plr.Room.Broadcast(new Network.Message.Game.PromotionCoinEventDropCoinAckMessage { Ammo = 10, Unk = 1000, Posions = (uint)amount });
              //  plr.Room.Broadcast(new NoticeAdminMessageAckMessage($"Coins have been spawned by {plr.Account.Nickname}"));
                return true;
            }

            public string Help()
            {
                return Name;
            }
        }
    }
}

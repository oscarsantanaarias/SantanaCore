using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Santana.Network;
using Santana.Network.Message.Game;
using Santana.Network.Services;

namespace Santana.Commands
{
    internal class JoinSalaCommand : ICommand
    {
        public JoinSalaCommand()
        {
            Name = "/joinsala";
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
            if (args.Length < 1 || !uint.TryParse(args[0], out var roomId))
            {
                CommandManager.Logger.Information(Help());
                return true;
            }

            Room target = null;
            foreach (var ch in server.ChannelManager)
            {
                if (ch.RoomManager._rooms.TryGetValue(roomId, out var found))
                {
                    target = found;
                    break;
                }
            }

            if (target == null)
            {
                CommandManager.Logger.Information($"[joinsala] no existe la sala {roomId}");
                return true;
            }

            var channel = target.RoomManager?.Channel;
            var channelService = new ChannelService();
            var roomService = new RoomService();
            var joined = 0;

            foreach (var candidate in server.Sessions.Values
                         .Select(x => ((GameSession)x).Player)
                         .Where(x => x != null && x.Room == null)
                         .ToList())
            {
                try
                {
                    var session = (GameSession)candidate.Session;
                    if (channel != null && candidate.Channel != channel)
                        channelService.ChannelEnterReq(session,
                            new ChannelEnterReqMessage { Channel = (uint)channel.Id });

                    await roomService.CGameRoomEnterReq(session,
                        new RoomEnterReqMessage { RoomId = roomId, Password = string.Empty, Unk1 = 0, Unk2 = 0 });
                    joined++;
                }
                catch (Exception ex)
                {
                    CommandManager.Logger.Information(
                        $"[joinsala] {candidate.Account.Nickname} no pudo entrar: {ex.GetType().Name}");
                }
            }

            CommandManager.Logger.Information(
                $"[joinsala] sala {roomId}: entraron {joined}, ahora hay {target.Players.Count}");
            return true;
        }

        public string Help()
        {
            return "/joinsala <roomId> - mete a todos los que estan online y sin sala pasando por los " +
                   "handlers reales de ChannelEnterReq y RoomEnterReq, para que el relay los acepte";
        }
    }
}

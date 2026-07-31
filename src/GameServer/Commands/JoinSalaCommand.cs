using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Santana.Network;

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

        public ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1 || !uint.TryParse(args[0], out var roomId))
            {
                CommandManager.Logger.Information(Help());
                return ValueTask.FromResult(true);
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
                return ValueTask.FromResult(true);
            }

            var channel = target.RoomManager?.Channel;
            var joined = 0;

            foreach (var candidate in server.Sessions.Values
                         .Select(x => ((GameSession)x).Player)
                         .Where(x => x != null && x.Room == null)
                         .ToList())
            {
                try
                {
                    // Paso 1: al canal de la sala (mismo flujo que ChannelEnterReq).
                    if (channel != null && candidate.Channel != channel)
                    {
                        candidate.Channel?.Leave(candidate);
                        channel.Join(candidate);
                    }

                    // Paso 2: a la sala.
                    target.Join(candidate);
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
            return ValueTask.FromResult(true);
        }

        public string Help()
        {
            return "/joinsala <roomId> - mete a todos los que estan online y sin sala: primero al canal " +
                   "de esa sala, despues a la sala";
        }
    }
}

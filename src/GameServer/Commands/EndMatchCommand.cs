using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Santana.Network;

namespace Santana.Commands
{
    internal class EndMatchCommand : ICommand
    {
        public EndMatchCommand()
        {
            Name = "/endsala";
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
                return new ValueTask<bool>(true);
            }
            Room target = null;
            foreach (var channel in GameServer.Instance.ChannelManager)
            {
                var room = channel.RoomManager[roomId];
                if (room != null)
                {
                    target = room;
                    break;
                }
            }
            if (target == null)
            {
                CommandManager.Logger.Information($"[endsala] room {roomId} no encontrado");
                return new ValueTask<bool>(true);
            }
            var rule = target.GameRuleManager?.GameRule;
            if (rule == null)
            {
                CommandManager.Logger.Information($"[endsala] room {roomId} sin gamerule");
                return new ValueTask<bool>(true);
            }
            // Disparar StartResult UNA sola vez: FullGame -> EnteringResult.
            // El Update del gamerule hace el countdown (ResultIn 9s) y avanza a Result solo,
            // igual que un clear normal. Dispararlo en loop saltaba el countdown -> pantalla azul.
            if (rule.StateMachine.CanFire(GameRuleStateTrigger.StartResult))
            {
                rule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
                CommandManager.Logger.Information($"[endsala] room {roomId}: StartResult, estado={rule.StateMachine.State}");
            }
            else
            {
                CommandManager.Logger.Information($"[endsala] room {roomId}: no se puede terminar desde estado={rule.StateMachine.State}");
            }
            return new ValueTask<bool>(true);
        }
        public string Help() => "/endsala <roomId>  -> termina el match del room y manda a result screen";
    }
}

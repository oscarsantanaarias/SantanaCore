using System.Collections.Generic;
using System.Threading.Tasks;
using Santana.Network;

namespace Santana.Commands
{
    internal interface ICommand
    {
        string Name { get; }
        bool AllowConsole { get; }
        SecurityLevel Permission { get; }
        IReadOnlyList<ICommand> SubCommands { get; }

        ValueTask<bool> Execute(GameServer server, Player plr, string[] args);
        string Help();
    }
}

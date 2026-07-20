using Santana.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Santana.Commands
{
    internal class HelpCommand : ICommand
    {
        public HelpCommand()
        {
            Name = "/help";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[0] { };
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            plr.SendConsoleMessage(S4Color.Green + ">>>> Game Master Help System <<<<<");
            plr.SendConsoleMessage(S4Color.Green + ">>>> For all Commands u need to whisper to server!!");
            plr.SendConsoleMessage(S4Color.Green + "> /whole_notice msg (Rank >GS<)");
            plr.SendConsoleMessage(S4Color.Green + "> admin rename/playerkick/roomkick (Rank >GS<) | level/seclevel/ap (Rank >GM<)");
            plr.SendConsoleMessage(S4Color.Green + "> /ban (for more infos just /ban) (Rank >GM<)");
            plr.SendConsoleMessage(S4Color.Green + "> game start (Rank >GM<) | pause (Rank >GM<//KINDABUGGED) | state StartResult/StartHalfTime (Rank >GM<)");
            plr.SendConsoleMessage(S4Color.Green + "> gm setmaster | td <player> <a/b> for Team Alpha/Beta");
            plr.SendConsoleMessage(S4Color.Green + "> to server: server coin > spawns coins<");

            return true;
        }

        public string Help()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                builder.Append(" ");
                builder.AppendLine(sub.Help());
            }
            return builder.ToString();
        }
    }
}

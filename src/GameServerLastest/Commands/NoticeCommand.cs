using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Santana.Network;

namespace Santana.Commands
{
    internal class NoticeCommand : ICommand
    {
        public NoticeCommand()
        {
            Name = "/notice";
            AllowConsole = false;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { };
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > notice message", NewChatType.All);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /notice message");
                return true;
            }

            var text = new StringBuilder();
            foreach (var word in args)
                text.Append(" " + word);

            var line = text.ToString();
            if (plr.Room != null)
                plr.Room.BroadcastNotice(line);
            else if (plr.Channel != null)
                plr.Channel.BroadcastNotice(line);
            return true;
        }

        public string Help()
        {
            var buffer = new StringBuilder();
            buffer.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                buffer.Append(" ");
                buffer.AppendLine(sub.Help());
            }

            return buffer.ToString();
        }
    }

    internal class WholeNoticeCommand : ICommand
    {
        public WholeNoticeCommand()
        {
            Name = "/whole_notice";
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
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > whole_notice message", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /whole_notice message");
                return true;
            }

            var text = new StringBuilder();
            foreach (var word in args)
                text.Append(" " + word);

            server.BroadcastNotice(text.ToString().Replace("/whole_notice", ""));
            return true;
        }

        public string Help()
        {
            var buffer = new StringBuilder();
            buffer.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                buffer.Append(" ");
                buffer.AppendLine(sub.Help());
            }

            return buffer.ToString();
        }
    }
}

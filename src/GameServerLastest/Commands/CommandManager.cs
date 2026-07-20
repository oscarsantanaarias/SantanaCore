namespace Santana.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Santana.Network;
    using Serilog;
    using Serilog.Core;

    internal class CommandManager
    {
        public static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(CommandManager));

        private readonly IList<ICommand> _registered = new List<ICommand>();

        public CommandManager(GameServer server)
        {
            Server = server;
        }

        public GameServer Server { get; }

        public CommandManager Add(ICommand cmd)
        {
            foreach (var existing in _registered)
            {
                if (existing.Name.Equals(cmd.Name, StringComparison.InvariantCultureIgnoreCase))
                    throw new Exception("Command " + cmd.Name + "already exists");
            }

            _registered.Add(cmd);
            return this;
        }

        public async ValueTask<bool> Execute(Player plr, string[] args)
        {
            return await Dispatch(plr, _registered, args);
        }

        private async ValueTask<bool> Dispatch(Player plr, IEnumerable<ICommand> pool, string[] args)
        {
            if (args.Length == 0)
                return false;

            var fromConsole = plr == null;

            ICommand match = null;
            foreach (var candidate in pool)
            {
                if (candidate.Name.Equals(args[0], StringComparison.InvariantCultureIgnoreCase))
                {
                    match = candidate;
                    break;
                }
            }

            if (match == null)
                return false;

            var rest = new string[args.Length - 1];
            Array.Copy(args, 1, rest, 0, rest.Length);

            if (fromConsole && !match.AllowConsole)
                return false;

            if (!fromConsole && plr.Account.SecurityLevel < match.Permission)
            {
                Logger.ForAccount(plr).Error("Refused {cmdName} - this account is not ranked high enough. Args were: {args}", match.Name,
                    string.Join(",", args));
                plr?.Channel?.SendMessage(plr, "System", "You don't have a right", NewChatType.All);
                plr.SendConsoleMessage(S4Color.Red + "You don't have a right");
                return false;
            }

            if (plr != null)
            {
                Logger.ForAccount(plr).Warning("Player ran {cmdName} with args: {args}",
                    match.Name, string.Join(",", args));
            }

            if (match.SubCommands.Count == 0)
            {
                if (await match.Execute(Server, plr, rest))
                    return true;

                if (plr == null)
                    Logger.Information(match.Help());
                else
                {
                    plr?.Channel?.SendMessage(plr, "System", "" + match.Help(), NewChatType.All);
                    plr.SendConsoleMessage(S4Color.Red + match.Help());
                }
                return true;
            }

            if (match.SubCommands.Count > 0 && args.Length < 2)
            {
                if (plr == null)
                    Logger.Information(match.Help());
                else
                {
                    plr?.Channel?.SendMessage(plr, "System", "" + match.Help(), NewChatType.All);
                    plr.SendConsoleMessage(S4Color.Red + match.Help());
                }
                return true;
            }

            return await Dispatch(plr, match.SubCommands, rest);
        }
    }
}

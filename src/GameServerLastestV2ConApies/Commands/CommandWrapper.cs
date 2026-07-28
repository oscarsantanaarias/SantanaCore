using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Santana.Network;

namespace Santana.Commands
{
  internal class CommandWrapper : ICommand
  {
    public CommandWrapper()
    {
      Name = "/cmd";
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
        plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
        plr.SendConsoleMessage(S4Color.Red + "> /cmd command");
        return true;
      }

      var handled = await GameServer.Instance.CommandManager.Execute(plr, args);
      if (handled)
        return true;

      if (plr == null)
        CommandManager.Logger.Information("No command matches what was typed");
      else
        plr.SendConsoleMessage(S4Color.Red + "Command is not implemented.");

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
  }
}

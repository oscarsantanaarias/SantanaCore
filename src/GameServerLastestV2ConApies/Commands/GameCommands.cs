using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Santana.Network;
using Santana.Network.Message.Game;

namespace Santana.Commands
{
  internal class GameCommands : ICommand
  {
    public GameCommands()
    {
      Name = "game";
      AllowConsole = false;
      Permission = SecurityLevel.Developer;
      SubCommands = new ICommand[] { new StateCommand(), new TimeCommand() };
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

    private class TimeCommand : ICommand
    {
      public TimeCommand()
      {
        Name = "time";
        AllowConsole = false;
        Permission = SecurityLevel.Developer;
        SubCommands = new ICommand[0];
      }

      public string Name { get; }
      public bool AllowConsole { get; }
      public SecurityLevel Permission { get; }
      public IReadOnlyList<ICommand> SubCommands { get; }

      public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
      {
        var room = plr.Room;
        if (room == null)
        {
          plr.SendConsoleMessage(S4Color.Red + "You're not inside a room");
          return true;
        }

        var inHalf = room.GameRuleState == GameRuleState.FirstHalf ||
                     room.GameRuleState == GameRuleState.SecondHalf;
        var cap = inHalf ? room.Options.TimeLimit / 2 : room.Options.TimeLimit;
        plr.SendConsoleMessage($"Current Time: {room.RoundTime}/{cap}");

        return true;
      }

      public string Help()
      {
        return Name + "[trigger]";
      }
    }

    private class StateCommand : ICommand
    {
      public StateCommand()
      {
        Name = "state";
        AllowConsole = false;
        Permission = SecurityLevel.Developer;
        SubCommands = new ICommand[0];
      }

      public string Name { get; }
      public bool AllowConsole { get; }
      public SecurityLevel Permission { get; }
      public IReadOnlyList<ICommand> SubCommands { get; }

      public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
      {
        var room = plr.Room;
        if (room == null)
        {
          plr.SendConsoleMessage(S4Color.Red + "You're not inside a room");
          return true;
        }

        var machine = room.GameRuleManager.GameRule.StateMachine;
        if (args.Length == 0)
        {
          plr.SendConsoleMessage($"Current state: {room.GameRuleState}");
          return true;
        }

        if (!Enum.TryParse(args[0], out GameRuleStateTrigger requested))
        {
          plr.SendConsoleMessage(
              $"{S4Color.Red}Invalid trigger! Available triggers: {string.Join(", ", machine.PermittedTriggers)}");
          return true;
        }

        if (!machine.CanFire(requested))
        {
          plr.SendConsoleMessage($"{S4Color.Red}This state cant be triggered now");
          return true;
        }

        machine.Fire(requested);
        room.Broadcast(
            new NoticeAdminMessageAckMessage(
                $"Current game state has been changed by {plr.Account.Nickname}"));

        return true;
      }

      public string Help()
      {
        return Name + "[trigger]";
      }
    }
  }
}

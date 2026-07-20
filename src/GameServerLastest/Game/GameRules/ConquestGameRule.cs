
using Santana.Network;

namespace Santana.Game.GameRules
{
  using System;
  using System.IO;
  using System.Linq;
  using Santana;
  using Santana.Network.Data.GameRule;
  using Santana.Network.Message.GameRule;

  internal class ConquestGameRule : GameRuleBase
  {
    public uint DropCount = 0;

    public ConquestGameRule(Room room)
        : base(room)
    {
      Briefing = new ConquestBriefing(this);

      StateMachine.Configure(GameRuleState.Waiting)
          .PermitIf(GameRuleStateTrigger.StartPrepare, GameRuleState.Preparing, CanStartGame);

      StateMachine.Configure(GameRuleState.Preparing)
          .Permit(GameRuleStateTrigger.StartGame, GameRuleState.FullGame);

      StateMachine.Configure(GameRuleState.FullGame)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

      StateMachine.Configure(GameRuleState.EnteringResult)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.StartResult, GameRuleState.Result);

      StateMachine.Configure(GameRuleState.Result)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting);
    }

    public override bool CountMatch => false;
    public override GameRule GameRule => GameRule.Horde;
    public override Briefing Briefing { get; }

    public override void Initialize()
    {
      var alphaSlots = (uint)Room.Options.PlayerLimit;
      var alphaSpectators = (uint)0;

      Room.TeamManager.Add(Team.Alpha, alphaSlots, alphaSpectators);
      Room.TeamManager.Add(Team.Beta, 0, 0);
      base.Initialize();
    }

    public override void Cleanup()
    {
      Room.TeamManager.Remove(Team.Alpha);
      Room.TeamManager.Remove(Team.Beta);

      base.Cleanup();
    }

    public override void Update(TimeSpan delta)
    {
      base.Update(delta);
      try
      {
        if (Room.GameState == GameState.Playing &&
            !StateMachine.IsInState(GameRuleState.EnteringResult) &&
            !StateMachine.IsInState(GameRuleState.Result) &&
            RoundTime >= TimeSpan.FromSeconds(5))
        {
          var roundLimit = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds);
          if (RoundTime >= roundLimit)
            StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }
      }
      catch (Exception ex)
      {
        Room.Logger.Error(ex.ToString());
      }
    }

    public override PlayerRecord GetPlayerRecord(Player plr)
    {
      return new ConquestPlayerRecord(plr);
    }

    private static ConquestPlayerRecord GetRecord(Player plr)
    {
      return (ConquestPlayerRecord)plr.RoomInfo.Stats;
    }

    private bool CanStartGame()
    {
      return true;
    }
  }

  internal class ConquestBriefing : Briefing
  {
    public ConquestBriefing(GameRuleBase gameRule)
        : base(gameRule)
    {
    }

    protected override void Serialize(BinaryWriter w, bool isResult)
    {
      base.Serialize(w, isResult);
    }
  }

  internal class ConquestPlayerRecord : PlayerRecord
  {
    public ConquestPlayerRecord(Player plr)
        : base(plr)
    {
    }

    public override uint TotalScore => GetTotalScore();

    public override void Serialize(BinaryWriter w, bool isResult)
    {
      base.Serialize(w, isResult);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
    }

    private uint GetTotalScore()
    {
      return 0;
    }

    public override int GetExpGain(out int bonusExp)
    {
      bonusExp = 0;
      return 0;
    }
  }
}


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

    public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
        LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
    {
      base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);
      if (killer == null || scoreTarget.PeerId.Category == PlayerCategory.Player)
        return;
      GetRecord(killer).KilledMonster++;
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

    public override uint TotalScore => KilledMonster;
    public uint KilledMonster { get; set; }

    public override void Serialize(BinaryWriter w, bool isResult)
    {
      base.Serialize(w, isResult);
      w.Write(KilledMonster);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
      w.Write(0);
    }

    public override int GetExpGain(out int bonusExp)
    {
      base.GetExpGain(out bonusExp);

      var expRates = Config.Instance.Game.BRExpRates;

      var contenders = Player.Room.TeamManager.Players
          .Where(plr => plr.RoomInfo.State == PlayerState.Waiting &&
                        plr.RoomInfo.Mode == PlayerGameMode.Normal)
          .ToArray();

      return (int)(TotalScore * expRates.ScoreFactor +
                    contenders.Length * expRates.PlayerCountFactor +
                    Player.RoomInfo.PlayTime.TotalMinutes * expRates.ExpPerMin);
    }
  }
}

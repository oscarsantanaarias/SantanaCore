
using Santana.Network;

namespace Santana.Game.GameRules
{
  using System;
  using System.IO;
  using System.Linq;
  using Santana;
  using Santana.Network.Data.GameRule;
  using Santana.Network.Message.GameRule;

  internal class DeathmatchTrainingGameRule : GameRuleBase
  {
    public DeathmatchTrainingGameRule(Room room)
        : base(room)
    {
      Briefing = new Briefing(this);

      StateMachine.Configure(GameRuleState.Waiting)
          .PermitIf(GameRuleStateTrigger.StartPrepare, GameRuleState.Preparing, CanStartGame);

      StateMachine.Configure(GameRuleState.Preparing)
          .Permit(GameRuleStateTrigger.StartGame, GameRuleState.FirstHalf);

      StateMachine.Configure(GameRuleState.FirstHalf)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.StartHalfTime, GameRuleState.EnteringHalfTime)
          .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

      StateMachine.Configure(GameRuleState.EnteringHalfTime)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.StartHalfTime, GameRuleState.HalfTime)
          .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

      StateMachine.Configure(GameRuleState.HalfTime)
          .SubstateOf(GameRuleState.Playing)
          .Permit(GameRuleStateTrigger.StartSecondHalf, GameRuleState.SecondHalf)
          .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);

      StateMachine.Configure(GameRuleState.SecondHalf)
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
    public override GameRule GameRule => GameRule.CombatTrainingDM;
    public override Briefing Briefing { get; }

    public override void Initialize()
    {
      var slotsPerTeam = (uint)Room.Options.PlayerLimit;

      Room.TeamManager.Add(Team.Alpha, slotsPerTeam, 0);
      Room.TeamManager.Add(Team.Beta, slotsPerTeam, 0);
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
        var teams = Room.TeamManager;

        var gameActive = Room.GameState == GameState.Playing &&
                         !StateMachine.IsInState(GameRuleState.EnteringResult) &&
                         !StateMachine.IsInState(GameRuleState.Result) &&
                         RoundTime >= TimeSpan.FromSeconds(5);

        if (!gameActive)
          return;

        var lowestActive = teams.Values.Min(team =>
            team.Keys.Count(participant =>
                participant.RoomInfo.State != PlayerState.Lobby &&
                participant.RoomInfo.State != PlayerState.Spectating));
        if (lowestActive == 0 && !Room.Options.IsFriendly)
          StateMachine.Fire(GameRuleStateTrigger.StartResult);

        var onFirstHalf = StateMachine.IsInState(GameRuleState.FirstHalf);
        var onSecondHalf = StateMachine.IsInState(GameRuleState.SecondHalf);
        if (!onFirstHalf && !onSecondHalf)
          return;

        var targetScore = onFirstHalf ? Room.Options.ScoreLimit / 2 : Room.Options.ScoreLimit;
        var advanceTrigger = onFirstHalf
            ? GameRuleStateTrigger.StartHalfTime
            : GameRuleStateTrigger.StartResult;

        if (teams.Values.Any(team => team.Score >= targetScore) &&
            StateMachine.CanFire(advanceTrigger))
          StateMachine.Fire(advanceTrigger);

        var halfDuration = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds / 2);
        if (RoundTime >= halfDuration &&
            StateMachine.CanFire(advanceTrigger))
          StateMachine.Fire(advanceTrigger);
      }
      catch (Exception ex)
      {
        Room.Logger.Error(ex.ToString());
      }
    }

    public override PlayerRecord GetPlayerRecord(Player plr)
    {
      return new DeathmatchTrainingPlayerRecord(plr);
    }

    private static DeathmatchTrainingPlayerRecord GetRecord(Player plr)
    {
      return (DeathmatchTrainingPlayerRecord)plr.RoomInfo.Stats;
    }

    public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
        LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
    {
      base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);

      if (!ScoreIsPlaying())
        return;

      var killedRealPlayer = (killer?.RoomInfo.PeerId.EqualSlot(scoreTarget) ?? false) &&
                             killer.RoomInfo.PeerId.IsPlayer();
      if (killedRealPlayer)
      {
        killer.RoomInfo.Team.Score++;
        return;
      }

      var opposingTeam = Room.TeamManager.FirstOrDefault(entry => entry.Key != Room.Master.RoomInfo.Team.Team);
      if (opposingTeam.Value != null)
        opposingTeam.Value.Score++;
    }

    private bool CanStartGame()
    {
      if (!StateMachine.IsInState(GameRuleState.Waiting))
        return false;

      return true;
    }
  }

  internal class DeathmatchTrainingPlayerRecord : PlayerRecord
  {
    public DeathmatchTrainingPlayerRecord(Player plr)
        : base(plr)
    {
    }

    public override uint TotalScore => GetTotalScore();

    public int HealAssists { get; set; }
    public int Unk { get; set; }
    public int Deaths2 { get; set; }
    public int Deaths3 { get; set; }

    public override void Serialize(BinaryWriter w, bool isResult)
    {
      base.Serialize(w, isResult);

      w.Write(Kills);
      w.Write(KillAssists);
      w.Write(HealAssists);
      w.Write(Deaths);
      w.Write(Unk);
      w.Write(Deaths2);
      w.Write(Deaths3);
    }

    public override void Reset()
    {
      base.Reset();

      HealAssists = 0;
      Unk = 0;
      Deaths2 = 0;
      Deaths3 = 0;
    }

    private uint GetTotalScore()
    {
      return (uint)(Kills * 2 + KillAssists + HealAssists * 2);
    }

    public override int GetExpGain(out int bonusExp)
    {
      base.GetExpGain(out bonusExp);

      var rates = Config.Instance.Game.DeathmatchExpRates;

      var contenders = Player.Room.TeamManager.Players
          .Where(member => member.RoomInfo.State == PlayerState.Waiting &&
                           member.RoomInfo.Mode == PlayerGameMode.Normal)
          .ToArray();

      var ranking = 1;
      foreach (var member in contenders.OrderByDescending(member => member.RoomInfo.Stats.TotalScore))
      {
        if (member == Player)
          break;

        ranking++;
        if (ranking > 3)
          break;
      }

      var placeBonus = 0f;
      switch (ranking)
      {
        case 1:
          placeBonus = rates.FirstPlaceBonus;
          break;

        case 2:
          placeBonus = rates.SecondPlaceBonus;
          break;

        case 3:
          placeBonus = rates.ThirdPlaceBonus;
          break;
      }

      return (int)(TotalScore * rates.ScoreFactor +
                    placeBonus +
                    contenders.Length * rates.PlayerCountFactor +
                    Player.RoomInfo.PlayTime.TotalMinutes * rates.ExpPerMin);
    }
  }
}

using Santana.Network;
namespace Santana.Game.GameRules
{
  using System;
  using System.IO;
  using System.Linq;
  using Santana.Network.Data.GameRule;
  using Santana.Network.Message.GameRule;
  using Santana.Game;
  using Santana.Game.GameRules;
  internal sealed class TouchdownTrainingGameRule : GameRuleBase
  {
    private static readonly TimeSpan CelebrationTime = TimeSpan.FromSeconds(12);
    private static readonly TimeSpan AssistWindow = TimeSpan.FromSeconds(10);
    private DateTime _reboundStamp { get; set; }
    private LongPeerId _currentBallHolder = 0;
    private Player _previousHolderPlayer;
    private LongPeerId _previousBallHolder = 0;
    private bool IsAssistValid => _previousBallHolder != 0 && DateTime.Now - _reboundStamp < AssistWindow &&
                                  !_previousBallHolder.EqualSlot(_currentBallHolder);
    private TimeSpan _celebrationElapsed;
    private bool _celebrating { get; set; }
    public override bool BlockPlaying => _celebrating;
    public TouchdownTrainingGameRule(Room room)
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
    public override bool CountMatch => true;
    public override GameRule GameRule => GameRule.CombatTrainingTD;
    public override Briefing Briefing { get; }
    public override void Initialize()
    {
      var teamSlots = (uint)Room.Options.PlayerLimit;
      Room.TeamManager.Add(Team.Alpha, teamSlots, 0);
      Room.TeamManager.Add(Team.Beta, teamSlots, 0);
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
        if (Room.GameState == GameState.Playing &&
            !StateMachine.IsInState(GameRuleState.EnteringResult) &&
            !StateMachine.IsInState(GameRuleState.Result) &&
            RoundTime >= TimeSpan.FromSeconds(5))
        {
          var fewestActive = teams.Values.Min(team =>
              team.Keys.Count(plr =>
                  plr.RoomInfo.State != PlayerState.Lobby &&
                  plr.RoomInfo.State != PlayerState.Spectating));
          if (fewestActive == 0 && !Room.Options.IsFriendly)
            StateMachine.Fire(GameRuleStateTrigger.StartResult);
          var inFirstHalf = StateMachine.IsInState(GameRuleState.FirstHalf);
          var inSecondHalf = StateMachine.IsInState(GameRuleState.SecondHalf);
          if (inFirstHalf || inSecondHalf)
          {
            var targetScore = inFirstHalf ? Room.Options.ScoreLimit / 2 : Room.Options.ScoreLimit;
            var advanceTrigger = inFirstHalf
                ? GameRuleStateTrigger.StartHalfTime
                : GameRuleStateTrigger.StartResult;
            if (teams.Values.Any(team => team.Score >= targetScore) &&
                StateMachine.CanFire(advanceTrigger))
              StateMachine.Fire(advanceTrigger);
            var halfTimeLimit = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds / 2);
            if (RoundTime >= halfTimeLimit &&
                StateMachine.CanFire(advanceTrigger))
              StateMachine.Fire(advanceTrigger);
          }
          if (_celebrating)
          {
            _celebrationElapsed += delta;
            if (!StateMachine.IsInState(GameRuleState.EnteringHalfTime) &&
                !StateMachine.IsInState(GameRuleState.HalfTime) &&
                !StateMachine.IsInState(GameRuleState.EnteringResult) &&
                !StateMachine.IsInState(GameRuleState.Result))
            {
              if (_celebrationElapsed >= CelebrationTime)
              {
                _celebrating = false;
                _celebrationElapsed = TimeSpan.Zero;
                Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.ResetRound, 0, 0, 0,
                    ""));
              }
            }
            else
            {
              _celebrating = false;
            }
          }
        }
      }
      catch (Exception e)
      {
        Room.Logger.Error(e.ToString());
      }
    }
    public override PlayerRecord GetPlayerRecord(Player plr)
    {
      return new TouchdownTrainingPlayerRecord(plr);
    }
    private static TouchdownTrainingPlayerRecord GetRecord(Player plr)
    {
      return (TouchdownTrainingPlayerRecord)plr.RoomInfo.Stats;
    }
    public static TDStats GetStats(Player plr)
    {
      return plr.stats.GetTDStats();
    }
    public void OnScoreOffense(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
        LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
    {
      var killerIsReal = (killer?.RoomInfo.PeerId.EqualSlot(scoreKiller) ?? false) &&
                             killer.RoomInfo.PeerId.IsPlayer();
      var targetIsReal = (target?.RoomInfo.PeerId.EqualSlot(scoreTarget) ?? false) &&
                             target.RoomInfo.PeerId.IsPlayer();
      var assistIsReal = (assist?.RoomInfo.PeerId.EqualSlot(scoreAssist) ?? false) &&
                             assist.RoomInfo.PeerId.IsPlayer();
      if (targetIsReal)
        Respawn(target);
      if (!ScoreIsPlaying())
        return;
      if (assist != null)
        Room.Broadcast(new ScoreOffenseAssistAckMessage(new ScoreAssistDto(scoreKiller, scoreAssist,
            scoreTarget, attackAttribute)));
      else
        Room.Broadcast(new ScoreOffenseAckMessage(new ScoreDto(scoreKiller, scoreTarget, attackAttribute)));
    }
    public void OnScoreDefense(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
        LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
    {
      var killerIsReal = (killer?.RoomInfo.PeerId.EqualSlot(scoreKiller) ?? false) &&
                             killer.RoomInfo.PeerId.IsPlayer();
      var targetIsReal = (target?.RoomInfo.PeerId.EqualSlot(scoreTarget) ?? false) &&
                             target.RoomInfo.PeerId.IsPlayer();
      var assistIsReal = (assist?.RoomInfo.PeerId.EqualSlot(scoreAssist) ?? false) &&
                             assist.RoomInfo.PeerId.IsPlayer();
      if (targetIsReal)
        Respawn(target);
      if (!ScoreIsPlaying())
        return;
      if (assist != null)
        Room.Broadcast(new ScoreDefenseAssistAckMessage(new ScoreAssistDto(scoreKiller, scoreAssist,
            scoreTarget, attackAttribute)));
      else
        Room.Broadcast(new ScoreDefenseAckMessage(new ScoreDto(scoreKiller, scoreTarget, attackAttribute)));
    }
    public void OnScoreRebound(Player newPlr, Player oldPlr, LongPeerId newid, LongPeerId oldId)
    {
      if (!ScoreIsPlaying())
        return;
      _currentBallHolder = newid;
      _reboundStamp = DateTime.Now;
      Room.Broadcast(new ScoreReboundAckMessage(newid, oldId));
    }
    public void OnScoreGoal(Player plr, LongPeerId scoreTarget)
    {
      if (!ScoreIsPlaying())
        return;
      _celebrating = true;
      var scorerIsReal = (plr?.RoomInfo.PeerId.EqualSlot(scoreTarget) ?? false) && plr.RoomInfo.PeerId.IsPlayer();
      var assisterIsReal = (_previousHolderPlayer?.RoomInfo.PeerId.EqualSlot(_previousBallHolder) ?? false) &&
                             _previousHolderPlayer.RoomInfo.PeerId.IsPlayer();
      if (scorerIsReal)
      {
        plr.RoomInfo.Team.Score++;
      }
      else
      {
        var opposingTeam = Room.TeamManager.FirstOrDefault(x => x.Key != Room.Master.RoomInfo.Team.Team);
        if (opposingTeam.Value != null)
          opposingTeam.Value.Score++;
      }
      if (IsAssistValid)
      {
        Room.Broadcast(new ScoreGoalAssistAckMessage(_currentBallHolder, _previousBallHolder));
      }
      else
      {
        Room.Broadcast(new ScoreGoalAckMessage(_currentBallHolder));
      }
      _currentBallHolder = 0;
      _previousBallHolder = 0;
      _previousHolderPlayer = null;
      var halfLength = TimeSpan.FromSeconds(Room.Options.TimeLimit.TotalSeconds / 2);
      var remaining = halfLength - RoundTime;
      if (remaining <= TimeSpan.FromSeconds(10))
        return;
      Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn,
          (ulong)CelebrationTime.TotalMilliseconds, 0, 0, ""));
      _celebrationElapsed = TimeSpan.Zero;
    }
    private bool CanStartGame()
    {
      if (!StateMachine.IsInState(GameRuleState.Waiting))
        return false;
      var teams = Room.TeamManager.Values.ToArray();
      if (Room.Options.IsFriendly)
        return true;
      if (teams.Any(team => team.Count == 0))
        return false;
      return teams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
    }
  }
  internal class TouchdownTrainingPlayerRecord : PlayerRecord
  {
    public TouchdownTrainingPlayerRecord(Player plr)
        : base(plr)
    {
    }
    public override uint TotalScore => GetTotalScore();
    public uint TDScore { get; set; }
    public uint TDAssistScore { get; set; }
    public uint OffenseScore { get; set; }
    public uint OffenseAssistScore { get; set; }
    public uint DefenseScore { get; set; }
    public uint DefenseAssistScore { get; set; }
    public uint HealScore { get; set; }
    public uint HealAssistScore { get; set; }
    public uint Unk2 { get; set; }
    public uint Unk3 { get; set; }
    public uint OffenseReboundScore { get; set; }
    public uint Unk4 { get; set; }
    public uint Unk5 { get; set; }
    public override void Serialize(BinaryWriter w, bool isResult)
    {
      base.Serialize(w, isResult);
      w.Write(TDScore);
      w.Write(TDAssistScore);
      w.Write(Kills);
      w.Write(KillAssists);
      w.Write(OffenseScore);
      w.Write(OffenseAssistScore);
      w.Write(DefenseScore);
      w.Write(DefenseAssistScore);
      w.Write(HealScore);
      w.Write(HealAssistScore);
      w.Write(Unk2);
      w.Write(Unk3);
      w.Write(OffenseReboundScore);
      w.Write(Unk4);
      w.Write(Unk5);
    }
    public override void Reset()
    {
      base.Reset();
      TDScore = 0;
      TDAssistScore = 0;
      OffenseScore = 0;
      OffenseAssistScore = 0;
      DefenseScore = 0;
      DefenseAssistScore = 0;
      HealScore = 0;
      OffenseReboundScore = 0;
      HealAssistScore = 0;
      Unk2 = 0;
      Unk3 = 0;
      Unk4 = 0;
      Unk5 = 0;
    }
    private uint GetTotalScore()
    {
      return TDScore * 10 + TDAssistScore * 5
                          + Kills * 2 + KillAssists
                          + OffenseScore * 4 + OffenseAssistScore * 2
                          + DefenseScore * 4 + DefenseAssistScore * 2
                          + HealScore * 2
                          + OffenseReboundScore * 2;
    }
    public override int GetExpGain(out int bonusExp)
    {
      base.GetExpGain(out bonusExp);
      var rates = Config.Instance.Game.TouchdownExpRates;
      var rank = 1;
      var contenders = Player.Room.TeamManager.Players
          .Where(plr => plr.RoomInfo.State == PlayerState.Waiting &&
                        plr.RoomInfo.Mode == PlayerGameMode.Normal)
          .ToArray();
      foreach (var plr in contenders.OrderByDescending(plr => plr.RoomInfo.Stats.TotalScore))
      {
        if (plr == Player)
          break;
        rank++;
        if (rank > 3)
          break;
      }
      var placementBonus = 0f;
      switch (rank)
      {
        case 1:
          placementBonus = rates.FirstPlaceBonus;
          break;
        case 2:
          placementBonus = rates.SecondPlaceBonus;
          break;
        case 3:
          placementBonus = rates.ThirdPlaceBonus;
          break;
      }
      var computedGain = TotalScore * rates.ScoreFactor +
                    placementBonus +
                    contenders.Length * rates.PlayerCountFactor +
                    Player.RoomInfo.PlayTime.TotalMinutes * rates.ExpPerMin;
      return (int)computedGain > 5000 ? 5000 : (int)computedGain;
    }
  }
}

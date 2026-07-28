using Santana.Network;
namespace Santana.Game.GameRules
{
    using System;
    using System.IO;
    using System.Linq;
    using Santana;
    using Santana.Network.Message.GameRule;
    internal class WarfareGameRule : GameRuleBase
    {
        private static readonly TimeSpan ResetRoundDelay = TimeSpan.FromSeconds(10);
        private TimeSpan _elapsedSinceQueenDown;
        private bool _queenDown;
        public override bool BlockPlaying => _queenDown;
        public WarfareGameRule(Room room)
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
        public override GameRule GameRule => GameRule.Warfare;
        public override bool CountMatch => true;
        public override Briefing Briefing { get; }
        public override void Initialize()
        {
            var perTeamPlayers = (uint)Room.Options.PlayerLimit / 2;
            var perTeamSpectators = (uint)Room.Options.SpectatorLimit / 2;
            Room.TeamManager.Add(Team.Alpha, perTeamPlayers, perTeamSpectators);
            Room.TeamManager.Add(Team.Beta, perTeamPlayers, perTeamSpectators);
            base.Initialize();
        }
        public override void Cleanup()
        {
            Room.TeamManager.Remove(Team.Alpha);
            Room.TeamManager.Remove(Team.Beta);
            base.Cleanup();
        }
        public override void ResetAfterSurrender()
        {
            _queenDown = false;
            _elapsedSinceQueenDown = TimeSpan.Zero;
            base.ResetAfterSurrender();
        }
        public override void OnRoomJoinCompleted(Player plr)
        {
            base.OnRoomJoinCompleted(plr);
            plr?.SendAsync(new GameRefreshGameRuleInfoAckMessage(Room.GameState, Room.SubGameState, Room.RoundTime));
            if (_queenDown)
            {
                var remaining = ResetRoundDelay - _elapsedSinceQueenDown;
                if (remaining > TimeSpan.Zero)
                {
                    plr?.SendAsync(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn, (ulong)remaining.TotalMilliseconds, 0, 0, ""));
                }
            }
        }
        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            try
            {
                var teams = Room.TeamManager;
                if (Room.GameState != GameState.Playing ||
                    StateMachine.IsInState(GameRuleState.EnteringResult) ||
                    StateMachine.IsInState(GameRuleState.Result) ||
                    RoundTime < TimeSpan.FromSeconds(5))
                    return;
                var fewestActive = teams.Values.Min(team => team.PlayersPlaying.Count());
                if (fewestActive == 0 && !Room.Options.IsFriendly)
                {
                    if (StateMachine.CanFire(GameRuleStateTrigger.StartResult))
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    return;
                }
                var inFirstHalf = StateMachine.IsInState(GameRuleState.FirstHalf);
                var inSecondHalf = StateMachine.IsInState(GameRuleState.SecondHalf);
                if (inFirstHalf || inSecondHalf)
                {
                    var halfScoreCap = inFirstHalf ? Room.Options.ScoreLimit / 2 : Room.Options.ScoreLimit;
                    var advanceTrigger = inFirstHalf
                        ? GameRuleStateTrigger.StartHalfTime
                        : GameRuleStateTrigger.StartResult;
                    if (teams.Values.Any(team => team.Score >= halfScoreCap) &&
                        StateMachine.CanFire(advanceTrigger))
                        StateMachine.Fire(advanceTrigger);
                    var halfTimeCap = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds / 2);
                    if (RoundTime >= halfTimeCap &&
                        StateMachine.CanFire(advanceTrigger))
                        StateMachine.Fire(advanceTrigger);
                }
                if (_queenDown && Room.GameRuleState != GameRuleState.HalfTime)
                {
                    _elapsedSinceQueenDown += delta;
                    if (_elapsedSinceQueenDown >= ResetRoundDelay)
                    {
                        _queenDown = false;
                        _elapsedSinceQueenDown = TimeSpan.Zero;
                        Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.ResetRound, 0, 0, 0, ""));
                    }
                }
                else
                {
                    _queenDown = false;
                }
            }
            catch (Exception ex)
            {
                Room.Logger.Error(ex.ToString());
            }
        }
        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new WarfarePlayerRecord(plr);
        }
        private static WarfarePlayerRecord GetRecord(Player plr)
        {
            return (WarfarePlayerRecord)plr.RoomInfo.Stats;
        }
        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);
            var killedByRealPlayer = (killer?.RoomInfo.PeerId.EqualSlot(scoreKiller) ?? false) &&
                         killer.RoomInfo.PeerId.IsPlayer();
            if (!killedByRealPlayer)
                Respawn(target);
            if (Room.TeamManager.Values.Any(team => team.Score >= Room.Options.ScoreLimit))
                return;
            if (!ScoreIsPlaying())
                return;
            if (!StateMachine.IsInState(GameRuleState.FirstHalf) &&
                !StateMachine.IsInState(GameRuleState.SecondHalf))
                return;
        }
        public virtual void OnScoreAIKill(Player plr, ulong unk)
        {
            if (!ScoreIsPlaying())
                return;
            var killerPeerRaw = (ushort)(unk >> 48);
            var killer = Room.Players.Values.FirstOrDefault(p =>
                p.RoomInfo?.PeerId?.PeerId != null &&
                (ushort)p.RoomInfo.PeerId.PeerId == killerPeerRaw) ?? plr;
            _queenDown = true;
            killer.RoomInfo.Team.Score++;
            GetRecord(killer).QueenKills++;
            Room.Broadcast(new ScoreAIKillAckMessage(unk));
            var midMatch = TimeSpan.FromSeconds(Room.Options.TimeLimit.TotalSeconds / 2);
            var remainingToMid = midMatch - RoundTime;
            if (remainingToMid <= TimeSpan.FromSeconds(10))
                return;
            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn,
                (ulong)ResetRoundDelay.TotalMilliseconds, 0, 0, ""));
        }
        public void RequestRoundReset()
        {
            if (!ScoreIsPlaying() || _queenDown)
                return;
            _queenDown = true;
            _elapsedSinceQueenDown = TimeSpan.Zero;
            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn,
                (ulong)ResetRoundDelay.TotalMilliseconds, 0, 0, ""));
        }
        private bool CanStartGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;
            var bothTeams = Room.TeamManager.Values.ToArray();
            if (Room.Options.IsFriendly)
                return true;
            if (bothTeams.Any(team => team.Count == 0))
                return false;
            return bothTeams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }
    }
    internal class WarfarePlayerRecord : PlayerRecord
    {
        public WarfarePlayerRecord(Player plr)
            : base(plr)
        {
        }
        public override uint TotalScore => GetTotalScore();
        public uint QueenKills { get; set; }
        public uint BonusKillAssists { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
        public int Unk10 { get; set; }
        public int Unk11 { get; set; }
        public int Unk12 { get; set; }
        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
            w.Write(QueenKills);
            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(Unk5);
            w.Write(Unk6);
            w.Write(Unk7);
            w.Write(Unk8);
            w.Write(Unk9);
            w.Write(Unk10);
            w.Write(Unk11);
            w.Write(Unk12);
        }
        public override void Reset()
        {
            base.Reset();
            Kills = 0;
            KillAssists = 0;
            QueenKills = 0;
            BonusKillAssists = 0;
            Unk5 = 0;
            Unk6 = 0;
            Unk7 = 0;
            Unk8 = 0;
            Unk9 = 0;
            Unk10 = 0;
            Unk11 = 0;
            Unk12 = 0;
        }
        private uint GetTotalScore()
        {
            return QueenKills * 10 + Kills * 2 + KillAssists;
        }
        public override int GetExpGain(out int bonusExp)
        {
            base.GetExpGain(out bonusExp);
            var expRates = Config.Instance.Game.BRExpRates;
            var ranking = 1;
            var contenders = Player.Room.TeamManager.Players
                .Where(plr => plr.RoomInfo.State == PlayerState.Waiting &&
                              plr.RoomInfo.Mode == PlayerGameMode.Normal)
                .ToArray();
            foreach (var contender in contenders.OrderByDescending(plr => plr.RoomInfo.Stats.TotalScore))
            {
                if (contender == Player)
                    break;
                ranking++;
                if (ranking > 3)
                    break;
            }
            var placementBonus = 0f;
            switch (ranking)
            {
                case 1:
                    placementBonus = expRates.FirstPlaceBonus;
                    break;
                case 2:
                    placementBonus = expRates.SecondPlaceBonus;
                    break;
                case 3:
                    placementBonus = expRates.ThirdPlaceBonus;
                    break;
            }
            return (int)(TotalScore * expRates.ScoreFactor +
                          placementBonus +
                          contenders.Length * expRates.PlayerCountFactor +
                          Player.RoomInfo.PlayTime.TotalMinutes * expRates.ExpPerMin);
        }
    }
}


using Santana.Network;

namespace Santana.Game.GameRules
{
    using System;
    using System.IO;
    using System.Linq;
    using Santana;
    using Santana.Network.Message.GameRule;

    internal class BattleRoyalGameRule : GameRuleBase
    {
        private const uint MinPlayersToStart = 2;

        private Player _leader;

        public BattleRoyalGameRule(Room room)
            : base(room)
        {
            Briefing = new Briefing(this);

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
                .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting)
                .OnEntry(() => { First = null; });
        }

        public override GameRule GameRule => GameRule.BattleRoyal;
        public override bool CountMatch => true;
        public override Briefing Briefing { get; }

        public Player First
        {
            get => _leader;
            private set
            {
                if (_leader == value)
                    return;

                _leader = value;

                if (StateMachine.IsInState(GameRuleState.Playing))
                {
                    Room.Broadcast(new FreeAllForChangeTheFirstAckMessage(_leader?.Account.Id ?? 0));
                }
            }
        }

        public override void OnIntrudeCompleted(Player plr)
        {
            plr.Session.SendAsync(new FreeAllForChangeTheFirstAckMessage(First?.Account.Id ?? 0));
        }

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

                if (!Room.Options.IsFriendly && teams.PlayersPlaying.Count() < MinPlayersToStart &&
                    !Room.Options.IsFriendly)
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);

                if (teams.PlayersPlaying.Any(plr => plr.RoomInfo.Stats.TotalScore >= Room.Options.ScoreLimit))
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);

                var timeCap = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds);
                if (RoundTime >= timeCap)
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);
            }
            catch (Exception ex)
            {
                Room.Logger.Error(ex.ToString());
            }
        }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new BattleRoyalPlayerRecord(plr);
        }

        private static BattleRoyalPlayerRecord GetRecord(Player plr)
        {
            return (BattleRoyalPlayerRecord)plr.RoomInfo.Stats;
        }

        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);

            if (Room.TeamManager.Values.Any(team => team.Score >= Room.Options.ScoreLimit))
                return;

            if (!ScoreIsPlaying())
                return;

            if (scoreTarget.PeerId.Category == PlayerCategory.Player)
            {
                if (target == First)
                {
                    GetRecord(killer).BonusKills++;
                    killer.stats.BattleRoyal.FirstKilled++;

                    if (assist != null)
                    {
                        GetRecord(assist).BonusKillAssists++;
                    }

                    GetRecord(killer).Kills--;
                    if (assist != null)
                    {
                        GetRecord(assist).KillAssists--;
                    }
                }
                else
                {
                    killer.stats.BattleRoyal.Kills++;
                }
            }

            First = GetFirst();
            First.AchieveMission.BRFirst++;
        }

        private Player GetFirst()
        {
            return Room.TeamManager.PlayersPlaying.OrderByDescending(x => x.RoomInfo.Stats.TotalScore).FirstOrDefault();
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
            if (Room.Players.Where(plr => plr.Value.RoomInfo.IsReady).ToArray().Length + 1 < MinPlayersToStart)
                return false;

            return bothTeams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }
    }

    internal class BattleRoyalPlayerRecord : PlayerRecord
    {
        public BattleRoyalPlayerRecord(Player plr)
            : base(plr)
        {
        }

        public override uint TotalScore => GetTotalScore();

        public uint BonusKills { get; set; }
        public uint BonusKillAssists { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
        public int Unk10 { get; set; }
        public int Unk11 { get; set; }

        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);

            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(BonusKills);
            w.Write(BonusKillAssists);
            w.Write(Unk5);
            w.Write(Unk6);
            w.Write(Unk7);
            w.Write(Unk8);
            w.Write(Unk9);
            w.Write(Unk10);
            w.Write(Unk11);
        }

        public override void Reset()
        {
            base.Reset();

            KillAssists = 0;
            BonusKills = 0;
            BonusKillAssists = 0;
            Unk5 = 0;
            Unk6 = 0;
            Unk7 = 0;
            Unk8 = 0;
            Unk9 = 0;
            Unk10 = 0;
            Unk11 = 0;
        }

        private uint GetTotalScore()
        {
            return Kills * 2 +
                   KillAssists +
                   BonusKills * 5 +
                   BonusKillAssists;
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

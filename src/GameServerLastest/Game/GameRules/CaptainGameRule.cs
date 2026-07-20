using System.Collections.Concurrent;
using Santana.Network;

namespace Santana.Game.GameRules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.GameRule;
    using Santana.Game;
    using Santana.Game.GameRules;

    internal class CaptainGameRule : GameRuleBase
    {
        private static readonly TimeSpan InterRoundDelay = TimeSpan.FromSeconds(7);
        private static readonly TimeSpan RoundDuration = TimeSpan.FromMinutes(3);

        public uint AlphaWins { get; set; }

        public uint BetaWins { get; set; }
        public float AlphaHealth { get; private set; }
        public float BetaHealth { get; private set; }
        public int CurrentRound { get; set; }

        private TimeSpan _sinceRoundEnded = TimeSpan.Zero;

        private TimeSpan _sinceRoundStarted = TimeSpan.Zero;

        private bool _betweenRounds = true;

        private TimeSpan _roundStartedAt = TimeSpan.Zero;
        private uint _alphaScoreAtRoundStart;
        private uint _betaScoreAtRoundStart;
        public readonly ConcurrentDictionary<Player, Team> PlayersCaptain = new ConcurrentDictionary<Player, Team>();

        public IEnumerable<Player> AlphaCaptains => PlayersCaptain.Where(entry => entry.Value == Team.Alpha).Select(entry => entry.Key);

        public IEnumerable<Player> BetaCaptains => PlayersCaptain.Where(entry => entry.Value == Team.Beta).Select(entry => entry.Key);

        public CaptainGameRule(Room room)
            : base(room)
        {
            Briefing = new CaptainBriefing(this);

            StateMachine.Configure(GameRuleState.Waiting)
                .PermitIf(GameRuleStateTrigger.StartPrepare, GameRuleState.Preparing, CanStartGame);

            StateMachine.Configure(GameRuleState.Preparing)
                .Permit(GameRuleStateTrigger.StartGame, GameRuleState.FullGame);

            StateMachine.Configure(GameRuleState.FullGame)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult)
                .OnEntry(NextRound);

            StateMachine.Configure(GameRuleState.EnteringResult)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.Result);

            StateMachine.Configure(GameRuleState.Result)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting);
        }

        public override GameRule GameRule => GameRule.Captain;

        public override Briefing Briefing { get; }

        public override bool CountMatch => true;

        public override bool BlockPlaying => _betweenRounds;

        public CaptainBriefing GetBriefing()
        {
            return (CaptainBriefing)Briefing;
        }

        public override void Initialize()
        {
            var slotsPerTeam = (uint)Room.Options.PlayerLimit / 2;
            var spectatorSlotsPerTeam = (uint)Room.Options.SpectatorLimit / 2;

            Room.TeamManager.Add(Team.Alpha, slotsPerTeam, spectatorSlotsPerTeam);
            Room.TeamManager.Add(Team.Beta, slotsPerTeam, spectatorSlotsPerTeam);
            base.Initialize();
        }

        public override void Cleanup()
        {
            Room.TeamManager.Remove(Team.Alpha);
            Room.TeamManager.Remove(Team.Beta);
            base.Cleanup();
        }

        public override void Reload()
        {
            try
            {
                AlphaWins = 0;
                BetaWins = 0;
                CurrentRound = (int)Room.TeamManager.Sum(entry => entry.Value.Score);
                PlayersCaptain.Clear();
            }
            catch { }
        }

        public override void ResetAfterSurrender()
        {
            _sinceRoundStarted = TimeSpan.Zero;
            _sinceRoundEnded = TimeSpan.Zero;
            base.ResetAfterSurrender();
        }

        public override void OnIntrudeCompleted(Player plr)
        {
            System.Console.WriteLine($"[CAP-HP] entra acc={plr.Account.Id} round={CurrentRound} capitanes={PlayersCaptain.Count} betweenRounds={_betweenRounds}");
            GetRecord(plr).IsCaptain = false;


            if (!_betweenRounds && Room.GameState == GameState.Playing)
            {
                System.Console.WriteLine($"[CAP-HP] round info -> acc={plr.Account.Id} ronda={CurrentRound} (mando {CurrentRound + 1})");
                plr.Session?.SendAsync(new CaptainCurrentRoundInfoAckMessage(CurrentRound + 1, RoundElapsed));
            }
        }

        public bool ValidPlayer(Player plr)
        {
            if (plr == null)
                return false;

            if (plr.Room != Room)
                return false;

            if (!plr.RoomInfo.HasLoaded)
                return false;

            return true;
        }

        public override void Update(TimeSpan delta)
        {
            base.Update(delta);

            var teams = Room.TeamManager;
            try
            {
                if (Room.GameState == GameState.Playing &&
                    !StateMachine.IsInState(GameRuleState.EnteringResult) &&
                    !StateMachine.IsInState(GameRuleState.Result) &&
                    RoundTime >= TimeSpan.FromSeconds(5))
                {
                    var fewestLoaded = teams.Values.Min(team =>
                        team.Keys.Count(plr => plr.RoomInfo.HasLoaded));

                    if (fewestLoaded == 0 && !Room.Options.IsFriendly)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);

                    if (teams.Values.Any(team => team.Score > Room.Options.ScoreLimit))
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);

                    if (CurrentRound - 1 > Room.Options.TimeLimit.Minutes)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);

                    if (CurrentRound > 0)
                    {
                        if (_betweenRounds)
                        {
                            _sinceRoundEnded += delta;

                            if (_sinceRoundEnded >= InterRoundDelay)
                            {
                                NextRound();
                            }
                        }
                        else
                        {
                            foreach (var entry in PlayersCaptain.Where(x => !ValidPlayer(x.Key)))
                            {
                                PlayersCaptain.TryRemove(entry.Key, out _);
                            }

                            if ((!AlphaCaptains.Any() || !BetaCaptains.Any() || !PlayersCaptain.Any()))
                                SubRoundEnd();

                            _sinceRoundStarted += delta;
                            if (_sinceRoundStarted >= RoundDuration)
                                SubRoundEnd();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Room.Logger.Error(e.ToString());
            }
        }

        private void NextRound()
        {
            if (!_betweenRounds)
                return;

            PlayersCaptain.Clear();
            foreach (var team in Room.TeamManager)
            {
                foreach (var plr in team.Value.NoSpectatorPlayers)
                {
                    var isPlaying = plr.RoomInfo.State != PlayerState.Lobby && plr.RoomInfo.HasLoaded;

                    GetRecord(plr).IsCaptain = isPlaying;

                    if (isPlaying)
                        PlayersCaptain.TryAdd(plr, team.Key);
                }
            }

            CurrentRound++;
            _roundStartedAt = RoundTime;

            AlphaHealth = 500;
            BetaHealth = 500;

            var alphaCount = Room.TeamManager[Team.Alpha].PlayersPlaying.Count();
            var betaCount = Room.TeamManager[Team.Beta].PlayersPlaying.Count();

            if (betaCount > 0 && alphaCount > betaCount)
            {
                BetaHealth = alphaCount * 500 / betaCount;
                if (Room.TeamManager[Team.Beta].Score > Room.TeamManager[Team.Alpha].Score)
                    BetaHealth = MathF.Floor(BetaHealth / 100) * 100;
                else
                    BetaHealth = MathF.Ceiling(BetaHealth / 100) * 100;
            }
            else if (alphaCount > 0 && betaCount > alphaCount)
            {
                AlphaHealth = betaCount * 500 / alphaCount;
                if (Room.TeamManager[Team.Alpha].Score > Room.TeamManager[Team.Beta].Score)
                    AlphaHealth = MathF.Floor(AlphaHealth / 100) * 100;
                else
                    AlphaHealth = MathF.Ceiling(AlphaHealth / 100) * 100;
            }

            System.Console.WriteLine($"[CAP-HP] NextRound round={CurrentRound} alphaCount={Room.TeamManager[Team.Alpha].PlayersPlaying.Count()} betaCount={Room.TeamManager[Team.Beta].PlayersPlaying.Count()} AlphaHealth={AlphaHealth} BetaHealth={BetaHealth} capitanes={PlayersCaptain.Count}");
            Room.Broadcast(new CaptainRoundCaptainLifeInfoAckMessage(Room.TeamManager.PlayersPlaying.Select(plr => new CaptainLifeDto(plr.Account.Id, plr.RoomInfo.Team.Team == Team.Alpha ? AlphaHealth : BetaHealth)).ToArray()));
            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.ResetRound, 0, 0, 0, ""));
            Room.Broadcast(new CaptainCurrentRoundInfoAckMessage(CurrentRound, RoundElapsed));
            _betweenRounds = false;

            _alphaScoreAtRoundStart = TeamScore(Team.Alpha);
            _betaScoreAtRoundStart = TeamScore(Team.Beta);
        }

        private void SubRoundEnd()
        {
            if (!ScoreIsPlaying())
                return;

            _sinceRoundEnded = TimeSpan.Zero;
            _sinceRoundStarted = TimeSpan.Zero;

            _betweenRounds = true;

            PlayerTeam roundWinner = null;

            if (AlphaCaptains.Count() > BetaCaptains.Count())
            {
                roundWinner = Room.TeamManager[Team.Alpha];
            }
            else if (BetaCaptains.Count() > AlphaCaptains.Count())
            {
                roundWinner = Room.TeamManager[Team.Beta];
            }
            else
            {
                var alpha = Room.TeamManager[Team.Alpha];
                var beta = Room.TeamManager[Team.Beta];

                var alphaScore = TeamScore(Team.Alpha) - _alphaScoreAtRoundStart;
                var betaScore = TeamScore(Team.Beta) - _betaScoreAtRoundStart;

                if (alphaScore > betaScore)
                    roundWinner = alpha;
                else if (betaScore > alphaScore)
                    roundWinner = beta;
                else
                    roundWinner = null;
            }

            if (roundWinner?.Team == Team.Alpha)
            {
                AlphaWins++;
            }

            if (roundWinner?.Team == Team.Beta)
            {
                BetaWins++;
            }

            if (roundWinner != null && roundWinner.Team != Team.Neutral)
            {
                roundWinner.Score++;
                foreach (var plr in roundWinner.PlayersPlaying)
                    GetRecord(plr).WinRound++;

                Room.Broadcast(new CaptainSubRoundWinAckMessage
                {
                    Unk1 = 3,
                    Unk2 = roundWinner.Team
                });

                Room.BroadcastBriefing();
            }

            if (Room.TeamManager.Values.Any(team => team.Score >= Room.Options.ScoreLimit))
                return;

            if (CurrentRound - 1 >= Room.Options.TimeLimit.Minutes)
                return;

            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn,
                (ulong)InterRoundDelay.TotalMilliseconds, 0, 0, ""));
        }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new CaptainPlayerRecord(plr);
        }

        private TimeSpan RoundElapsed
        {
            get
            {
                var elapsed = RoundTime - _roundStartedAt;
                return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
            }
        }

        private uint TeamScore(Team team)
        {
            try
            {
                var side = Room.TeamManager[team];
                if (side == null)
                    return 0;

                return (uint)side.PlayersPlaying
                    .Where(plr => plr?.RoomInfo?.Stats != null)
                    .Sum(plr => plr.RoomInfo.Stats.TotalScore);
            }
            catch
            {
                return 0;
            }
        }

        private static CaptainPlayerRecord GetRecord(Player plr)
        {
            return (CaptainPlayerRecord)plr.RoomInfo.Stats;
        }

        public override void OnScoreHeal(Player plr, LongPeerId scoreTarget)
        {
            base.OnScoreHeal(plr, scoreTarget);

            if (!ScoreIsPlaying())
                return;

            GetRecord(plr).HealPoints++;
        }

        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);

            if (!ScoreIsPlaying())
                return;

            if (scoreTarget.PeerId.Category == PlayerCategory.Player)
            {
                var victimTeam = target?.RoomInfo?.Team;
                if (victimTeam != null && PlayersCaptain.TryRemove(target, out _))
                {
                    var slayerTeam = killer?.RoomInfo?.Team;
                    if (slayerTeam != null)
                    {
                        GetRecord(killer).CaptainKills++;
                        if (assist != null)
                            GetRecord(assist).CaptainKillAssists++;
                    }
                }
            }
        }

        public override void OnScoreSuicide(Player target, LongPeerId scoreTarget, AttackAttribute icon)
        {
            base.OnScoreSuicide(target, scoreTarget, icon);

            if (!ScoreIsPlaying())
                return;

            if (scoreTarget.PeerId.Category == PlayerCategory.Player)
            {
                var victimTeam = target?.RoomInfo?.Team;
                if (victimTeam != null)
                {
                    if (PlayersCaptain.TryRemove(target, out _))
                        GetRecord(target).IsCaptain = false;

                    GetRecord(target).Suicides++;
                }
            }
        }

        private bool CanStartGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;

            if (Room.Options.IsFriendly)
                return true;

            var teams = Room.TeamManager.Values.ToArray();
            if (teams.Any(team => team.Count == 0))
                return false;

            return teams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }
    }

    internal class CaptainBriefing : Briefing
    {
        public CaptainBriefing(GameRuleBase ruleBase)
            : base(ruleBase)
        {
        }
    }

    internal class CaptainPlayerRecord : PlayerRecord
    {
        public CaptainPlayerRecord(Player plr)
            : base(plr)
        {
        }

        public override uint TotalScore
        {
            get
            {
                var earned = 5 * (WinRound + CaptainKills) + CaptainKillAssists +
                             2 * Kills + KillAssists + HealPoints;
                return Suicides >= earned ? 0 : earned - Suicides;
            }
        }
        public uint CaptainKills { get; set; }
        public uint CaptainKillAssists { get; set; }
        public uint HealPoints { get; set; }
        public uint WinRound { get; set; }
        public bool IsCaptain { get; set; }

        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(HealPoints);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(CaptainKillAssists);
            w.Write(CaptainKills);
            w.Write(WinRound);
            w.Write(Deaths);
            w.Write(IsCaptain);
            w.Write(0);
        }

        public override void Reset()
        {
            base.Reset();
            CaptainKills = 0;
            CaptainKillAssists = 0;
            HealPoints = 0;
            WinRound = 0;
            IsCaptain = false;
        }

        public override int GetExpGain(out int bonusExp)
        {
            base.GetExpGain(out bonusExp);

            var rates = Config.Instance.Game.CaptainExpRates;
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

            return (int)(TotalScore * rates.ScoreFactor +
                          placementBonus +
                          contenders.Length * rates.PlayerCountFactor +
                          Player.RoomInfo.PlayTime.TotalMinutes * rates.ExpPerMin);
        }
    }
}

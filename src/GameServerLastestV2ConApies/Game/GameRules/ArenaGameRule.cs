using Santana.Network;
namespace Santana.Game.GameRules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.IO;
    using Santana;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.GameRule;
    using Serilog;
    using Serilog.Core;
    internal class ArenaGameRule : GameRuleBase
    {
        private static readonly ILogger Journal =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ArenaGameRule));
        private static readonly TimeSpan HoldKillToResult = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan HoldResultToReady = TimeSpan.FromSeconds(8);
        private static readonly TimeSpan HoldReadyToGo = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan LiveRoundLength = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan FirstRoundGrace = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan DrawHpTimeout = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan LeaderBannerHold = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan HpSnapshotLead = TimeSpan.FromSeconds(1.5);
        private enum RoundStage { Setup, Fighting, AfterKill, Verdict, Countdown, AwaitDrawHp, LeaderBanner }
        private RoundStage _stage = RoundStage.Setup;
        private TimeSpan _stageElapsed = TimeSpan.Zero;
        private Team? _roundWinner;
        private bool _inLeaderDuel;
        private bool _swapSidesNext;
        private readonly Dictionary<ulong, float> _reportedHp = new Dictionary<ulong, float>();
        private readonly Dictionary<Team, uint> _supportBarByTeam = new Dictionary<Team, uint>();
        private bool _hpSnapshotSent;
        public int CurrentRound { get; set; }
        public Player PlayerAlphaBattle { get; set; }
        public Player PlayerBetaBattle { get; set; }
        public ArenaGameRule(Room room)
            : base(room)
        {
            Briefing = new ArenaBriefing(this);
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
        public override GameRule GameRule => GameRule.Arena;
        public override Briefing Briefing { get; }
        public override bool CountMatch => true;
        public override bool BlockPlaying => _stage != RoundStage.Fighting;
        private uint RoundsToWin => Room.Options.ScoreLimit > 0 ? (uint)Room.Options.ScoreLimit : 10;
        private uint SwapPoint => RoundsToWin / 2;
        public ArenaBriefing GetBriefing()
        {
            return (ArenaBriefing)Briefing;
        }
        public override void Initialize()
        {
            var halfPlayers = (uint)Room.Options.PlayerLimit / 2;
            var halfSpectators = (uint)Room.Options.SpectatorLimit / 2;
            Room.TeamManager.Add(Team.Alpha, halfPlayers, halfSpectators);
            Room.TeamManager.Add(Team.Beta, halfPlayers, halfSpectators);
            Room.Options.TimeLimit = LiveRoundLength;
            base.Initialize();
            Journal.Information("[ARENA] Initialize: ScoreLimit(option)={s} -> MatchScore={m}, HalfPoint={h}",
                Room.Options.ScoreLimit, RoundsToWin, SwapPoint);
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
                CurrentRound = (int)Room.TeamManager.Sum(x => x.Value.Score);
                _stage = RoundStage.Setup;
                _stageElapsed = TimeSpan.Zero;
                _roundWinner = null;
                _hpSnapshotSent = false;
                _inLeaderDuel = false;
                _swapSidesNext = false;
                _supportBarByTeam.Clear();
                PlayerAlphaBattle = null;
                PlayerBetaBattle = null;
            }
            catch { }
        }
        public override void ResetAfterSurrender()
        {
            _stage = RoundStage.Setup;
            _stageElapsed = TimeSpan.Zero;
            base.ResetAfterSurrender();
        }
        public override void OnRoomJoinCompleted(Player plr)
        {
            base.OnRoomJoinCompleted(plr);
            Trace($"-> {plr.Account.Nickname} Arena_Set_Game_Option_Ack(3100) Unk=3 (on join)");
            plr.SendAsync(new ArenaSetGameOptionAckMessage(3));
            SyncRoster();
            foreach (var occupant in Room.TeamManager.Players)
            {
                if (occupant.RoomInfo?.Team == null || occupant.RoomInfo.Team.Team == 0)
                    continue;
                var tag = new RoomEnterPlayerForBookNameTagsAckMessage
                {
                    AccountId = occupant.Account.Id,
                    Team = occupant.RoomInfo.Team.Team,
                    PlayerGameMode = occupant.RoomInfo.Mode,
                    Exp = occupant.TotalExperience,
                    Nickname = occupant.Account.Nickname,
                    Unk1 = occupant.NameTag,
                    Unk2 = (byte)(occupant.NameTag > 0 ? 1 : 0)
                };
                foreach (var target in Room.TeamManager.Players)
                {
                    if (target == occupant)
                        continue;
                    target.Session?.SendAsync(tag);
                }
                Trace($"BROADCAST RoomEnterPlayerForBookNameTags(member={occupant.Account.Nickname} team={occupant.RoomInfo.Team.Team}) -> all");
            }
        }
        private void SyncRoster()
        {
            var alphaIds = Room.TeamManager[Team.Alpha].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            var betaIds = Room.TeamManager[Team.Beta].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            Trace($"BROADCAST Sync_First_Arena_Battle_Idx(3098) roster alpha=[{string.Join(",", alphaIds)}] beta=[{string.Join(",", betaIds)}]");
            Room.Broadcast(new SyncFirstArenaBattleIdxMessage(alphaIds, betaIds));
        }
        public override void OnIntrudeCompleted(Player plr)
        {
            base.OnIntrudeCompleted(plr);
            var alphaReady = Room.TeamManager[Team.Alpha].PlayersPlaying.Any();
            var betaReady = Room.TeamManager[Team.Beta].PlayersPlaying.Any();
            if (!alphaReady || !betaReady)
            {
                Trace($"OnIntrude {plr.Account.Nickname}: roster incompleto (alphaReady={alphaReady} betaReady={betaReady}) -> NO mando Sync_Idx todavia");
                return;
            }

            foreach (var p in Room.TeamManager.PlayersPlaying)
            {
                if (p == PlayerAlphaBattle || p == PlayerBetaBattle)
                    continue;
                SendBattleIndex(p);
            }
        }
        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new ArenaPlayerRecord(plr);
        }
        private static ArenaPlayerRecord Stats(Player plr)
        {
            return (ArenaPlayerRecord)plr.RoomInfo.Stats;
        }
        public bool ValidPlayer(Player plr)
        {
            return plr != null && plr.Room == Room && plr.RoomInfo.HasLoaded;
        }
        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            try
            {
                if (Room.GameState != GameState.Playing)
                    return;
                if (StateMachine.IsInState(GameRuleState.EnteringResult) ||
                    StateMachine.IsInState(GameRuleState.Result))
                    return;
                if (StateMachine.IsInState(GameRuleState.EnteringHalfTime) ||
                    StateMachine.IsInState(GameRuleState.HalfTime))
                {
                    _stage = RoundStage.Setup;
                    return;
                }
                var playingHalf = StateMachine.IsInState(GameRuleState.FirstHalf) ||
                                  StateMachine.IsInState(GameRuleState.SecondHalf);
                if (!playingHalf)
                    return;
                var teams = Room.TeamManager;
                var loadedFloor = teams.Values.Min(team => team.Keys.Count(plr => plr.RoomInfo.HasLoaded));
                if (loadedFloor == 0)
                {
                    Journal.Information("[ARENA] A team is empty -> result");
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    return;
                }
                _stageElapsed += delta;
                switch (_stage)
                {
                    case RoundStage.Setup:
                        if (RoundTime < FirstRoundGrace)
                            break;
                        PickFighters();
                        if (ValidPlayer(PlayerAlphaBattle) && ValidPlayer(PlayerBetaBattle))
                            EnterCountdown();
                        break;
                    case RoundStage.Fighting:
                        if (!ValidPlayer(PlayerAlphaBattle) && !ValidPlayer(PlayerBetaBattle))
                        {
                            _roundWinner = null;
                            EnterAfterKill();
                        }
                        else if (!ValidPlayer(PlayerAlphaBattle))
                        {
                            Journal.Information("[ARENA] Alpha fighter gone -> Beta wins round");
                            _roundWinner = Team.Beta;
                            EnterAfterKill();
                        }
                        else if (!ValidPlayer(PlayerBetaBattle))
                        {
                            Journal.Information("[ARENA] Beta fighter gone -> Alpha wins round");
                            _roundWinner = Team.Alpha;
                            EnterAfterKill();
                        }
                        else
                        {
                            if (!_hpSnapshotSent && _stageElapsed >= LiveRoundLength - HpSnapshotLead)
                            {
                                _hpSnapshotSent = true;
                                _reportedHp.Clear();
                                Trace("BROADCAST Arena_Draw_HealthPoint_Req(3104) (HP snapshot ~1.5s before time-up)");
                                Room.Broadcast(new ArenaDrawHealthPointReqMessage());
                            }
                            if (_stageElapsed >= LiveRoundLength)
                                ResolveByHp();
                        }
                        break;
                    case RoundStage.AwaitDrawHp:
                        if (_stageElapsed >= DrawHpTimeout)
                        {
                            Journal.Information("[ARENA] HP answers timed out -> resolving with what we have");
                            ResolveByHp();
                        }
                        break;
                    case RoundStage.AfterKill:
                        if (_stageElapsed >= HoldKillToResult)
                            ShowResult();
                        break;
                    case RoundStage.Verdict:
                        if (_stageElapsed >= HoldResultToReady)
                        {
                            var topScore = teams.Values.Max(t => t.Score);
                            Journal.Information("[ARENA] Result check: maxScore={s} MatchScore={m} HalfPoint={h} state={st} pendingHT={p} showdown={sd}",
                                topScore, RoundsToWin, SwapPoint, StateMachine.State, _swapSidesNext, _inLeaderDuel);
                            if (topScore >= RoundsToWin &&
                                StateMachine.CanFire(GameRuleStateTrigger.StartResult))
                            {
                                Journal.Information("[ARENA] Score {s} reached {m} -> result", topScore, RoundsToWin);
                                StateMachine.Fire(GameRuleStateTrigger.StartResult);
                                break;
                            }
                            if (StateMachine.IsInState(GameRuleState.FirstHalf) &&
                                topScore >= SwapPoint &&
                                StateMachine.CanFire(GameRuleStateTrigger.StartHalfTime))
                            {
                                Journal.Information("[ARENA] Score {s} reached half {h} -> half-time", topScore, SwapPoint);
                                StateMachine.Fire(GameRuleStateTrigger.StartHalfTime);
                                _stage = RoundStage.Setup;
                                break;
                            }
                            EnterCountdown();
                        }
                        break;
                    case RoundStage.Countdown:
                        if (_stageElapsed >= HoldReadyToGo)
                            FireGo();
                        break;
                    case RoundStage.LeaderBanner:
                        if (_stageElapsed >= LeaderBannerHold)
                            EnterCountdown();
                        break;
                }
            }
            catch (Exception e)
            {
                Room.Logger.Error(e.ToString());
            }
        }
        private void PickFighters()
        {
            if (!ValidPlayer(PlayerAlphaBattle))
                PlayerAlphaBattle = Room.TeamManager[Team.Alpha].PlayersPlaying
                    .OrderByDescending(x => x.Level).FirstOrDefault();
            if (!ValidPlayer(PlayerBetaBattle))
                PlayerBetaBattle = Room.TeamManager[Team.Beta].PlayersPlaying
                    .OrderByDescending(x => x.Level).FirstOrDefault();
        }
        private void PickLeaders()
        {
            PlayerAlphaBattle = Room.TeamManager[Team.Alpha].PlayersPlaying
                .OrderByDescending(p => Stats(p).RoundsWon).ThenByDescending(p => p.Level).FirstOrDefault();
            PlayerBetaBattle = Room.TeamManager[Team.Beta].PlayersPlaying
                .OrderByDescending(p => Stats(p).RoundsWon).ThenByDescending(p => p.Level).FirstOrDefault();
        }
        private void StartLeaderShowdown()
        {
            _inLeaderDuel = true;
            PickLeaders();
            Journal.Information("[ARENA] LEADER SHOWDOWN: {a} vs {b}",
                PlayerAlphaBattle?.Account.Nickname ?? "-", PlayerBetaBattle?.Account.Nickname ?? "-");
            Room.Broadcast(new ArenaLeaderShowdwonMessage());
            _stage = RoundStage.LeaderBanner;
            _stageElapsed = TimeSpan.Zero;
        }
        private void EnterAfterKill()
        {
            _stage = RoundStage.AfterKill;
            _stageElapsed = TimeSpan.Zero;
        }
        private void StartDrawResolution()
        {
            _reportedHp.Clear();
            Trace("BROADCAST Arena_Draw_HealthPoint_Req(3104) (no fields, asking fighters' HP)");
            Room.Broadcast(new ArenaDrawHealthPointReqMessage());
            _stage = RoundStage.AwaitDrawHp;
            _stageElapsed = TimeSpan.Zero;
        }
        private void ResolveByHp()
        {
            if (_stage != RoundStage.Fighting)
                return;
            float alphaHp = 0;
            float betaHp = 0;
            var alphaReported = PlayerAlphaBattle != null && _reportedHp.TryGetValue(PlayerAlphaBattle.Account.Id, out alphaHp);
            var betaReported = PlayerBetaBattle != null && _reportedHp.TryGetValue(PlayerBetaBattle.Account.Id, out betaHp);
            Journal.Information("[ARENA] ResolveByHp alphaHp={a}(rep={ah}) betaHp={b}(rep={bh})", alphaHp, alphaReported, betaHp, betaReported);
            const float HpTieTolerance = 1.0f;
            if (!alphaReported || !betaReported)
                _roundWinner = null;
            else if (alphaHp > betaHp + HpTieTolerance)
                _roundWinner = Team.Alpha;
            else if (betaHp > alphaHp + HpTieTolerance)
                _roundWinner = Team.Beta;
            else
                _roundWinner = null;
            EnterAfterKill();
        }
        private void ShowResult()
        {
            if (_roundWinner == null)
            {

                BroadcastFightersOnly();
                Room.TeamManager[Team.Alpha].Score++;
                Room.TeamManager[Team.Beta].Score++;
                RotateFighter(Team.Alpha);
                RotateFighter(Team.Beta);
                Journal.Information("[ARENA] Round end -> DRAW (no points). Score A={a} B={b}",
                    Room.TeamManager[Team.Alpha].Score, Room.TeamManager[Team.Beta].Score);
                Trace($"BROADCAST GameChangeSubState(GameTimeState=15)=DRAW  [Score A={Room.TeamManager[Team.Alpha].Score} B={Room.TeamManager[Team.Beta].Score}]");
                Room.Broadcast(new GameChangeSubStateAckMessage((GameTimeState)15));
            }
            else
            {
                var champTeam = _roundWinner.Value;
                var beatenTeam = champTeam == Team.Alpha ? Team.Beta : Team.Alpha;
                var champ = champTeam == Team.Alpha ? PlayerAlphaBattle : PlayerBetaBattle;
                var beaten = beatenTeam == Team.Alpha ? PlayerAlphaBattle : PlayerBetaBattle;

                BroadcastFightersOnly();
                Room.TeamManager[champTeam].Score++;
                if (ValidPlayer(champ))
                    Stats(champ).RoundsWon++;
                if (ValidPlayer(beaten))
                    Stats(beaten).BattleWins = 0;
                RotateFighter(beatenTeam);
                if (ValidPlayer(champ) && Stats(champ).BattleWins > 2)
                {
                    Journal.Information("[ARENA] {nick} won 3 in a row -> rotates out",
                        champ.Account.Nickname);
                    Stats(champ).BattleWins = 0;
                    RotateFighter(champTeam);
                }
                Journal.Information("[ARENA] {result} wins round. Score A={a} B={b}",
                    champTeam, Room.TeamManager[Team.Alpha].Score, Room.TeamManager[Team.Beta].Score);
                var champId = ValidPlayer(champ) ? champ.Account.Id : 0;
                Trace($"BROADCAST Score_Arena_BattlePlayer_Leave(3102) WinPoint={(byte)champTeam} AccountId={champId} Unk1=0");
                Room.Broadcast(new ScoreArenaBattlePlayerLeaveMessage
                {
                    WinPoint = (byte)champTeam,
                    AccountId = champId,
                    Unk1 = 0
                });
                Trace($"BROADCAST GameChangeSubState(GameTimeState=16)=WIN/LOSE winner={champTeam}");
                Room.Broadcast(new GameChangeSubStateAckMessage((GameTimeState)16));
            }
            Trace($"BROADCAST GameEvent.NextRoundIn ms={(ulong)(HoldResultToReady + HoldReadyToGo).TotalMilliseconds}");
            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.NextRoundIn,
                (ulong)(HoldResultToReady + HoldReadyToGo).TotalMilliseconds, 0, 0, ""));

            if (_inLeaderDuel)
            {
                _inLeaderDuel = false;
                _swapSidesNext = true;
            }
            _stage = RoundStage.Verdict;
            _stageElapsed = TimeSpan.Zero;
        }
        private void EnterCountdown()
        {
            CurrentRound++;
            PickFighters();
            ReviveFighters();
            Journal.Information("[ARENA] Ready round #{round} alpha={a} beta={b}",
                CurrentRound, PlayerAlphaBattle?.Account.Nickname ?? "-",
                PlayerBetaBattle?.Account.Nickname ?? "-");
            BroadcastBattleIndex();
            if (IsTeamLeader(PlayerAlphaBattle, Team.Alpha) && IsTeamLeader(PlayerBetaBattle, Team.Beta))
            {

                Trace("BROADCAST Arena_LeaderShowdwon(3103) (leader showdown banner)");
                Room.Broadcast(new ArenaLeaderShowdwonMessage());
            }
            Trace($"BROADCAST GameChangeSubState(GameTimeState=14)=READY round={CurrentRound} alpha={PlayerAlphaBattle?.Account.Id ?? 0} beta={PlayerBetaBattle?.Account.Id ?? 0}");
            Room.Broadcast(new GameChangeSubStateAckMessage((GameTimeState)14));
            _stage = RoundStage.Countdown;
            _stageElapsed = TimeSpan.Zero;
        }
        private bool IsTeamLeader(Player plr, Team team)
        {
            if (!ValidPlayer(plr))
                return false;
            var best = Room.TeamManager[team].PlayersPlaying
                .OrderByDescending(p => Stats(p).RoundsWon)
                .ThenByDescending(p => p.Level)
                .FirstOrDefault();
            return plr == best;
        }
        private void FireGo()
        {
            foreach (var fighter in new[] { PlayerAlphaBattle, PlayerBetaBattle })
            {
                if (ValidPlayer(fighter))
                    fighter.RoomInfo.State = fighter.RoomInfo.Mode == PlayerGameMode.Normal
                        ? PlayerState.Alive : PlayerState.Spectating;
            }
            Trace("BROADCAST GameEvent.ResetRound=GO");
            Room.Broadcast(new GameEventMessageAckMessage(GameEventMessage.ResetRound, 0, 0, 0, ""));
            _hpSnapshotSent = false;
            _stage = RoundStage.Fighting;
            _stageElapsed = TimeSpan.Zero;
        }
        private void ReviveFighters()
        {
            ReviveOne(PlayerAlphaBattle);
            ReviveOne(PlayerBetaBattle);
        }
        private void ReviveOne(Player plr)
        {
            if (!ValidPlayer(plr))
                return;
            var floorHp = Room.Options.HP > 0 ? Room.Options.HP : 100f;
            var itemHp = plr.CharacterManager?.CurrentCharacter?.GetHP() ?? 0u;
            plr.SendAsync(new AdminChangeHPAckMessage { Value = floorHp + itemHp });
        }
        private void RotateFighter(Team team)
        {
            var roster = Room.TeamManager[team];
            var outgoing = team == Team.Alpha ? PlayerAlphaBattle : PlayerBetaBattle;
            var incoming = roster.PlayersPlaying
                               .Where(x => x != outgoing)
                               .OrderByDescending(x => x.Level)
                               .FirstOrDefault()
                           ?? outgoing;
            if (team == Team.Alpha)
                PlayerAlphaBattle = incoming;
            else
                PlayerBetaBattle = incoming;
        }
        public override void Respawn(Player victim)
        {
            if (victim != null)
                victim.RoomInfo.State = PlayerState.Dead;
        }
        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            if (_stage == RoundStage.AfterKill && _roundWinner != null && target != null &&
                _stageElapsed < TimeSpan.FromSeconds(1))
            {
                var champFighter = _roundWinner == Team.Alpha ? PlayerAlphaBattle : PlayerBetaBattle;
                if (target == champFighter)
                {
                    if (ValidPlayer(champFighter) && Stats(champFighter).BattleWins > 0)
                        Stats(champFighter).BattleWins--;
                    _roundWinner = null;
                    Journal.Information("[ARENA] Double KO -> draw");
                }
                return;
            }
            if (!ScoreIsPlaying() || _stage != RoundStage.Fighting || target == null)
                return;
            Respawn(target);
            if (ValidPlayer(killer)) { killer.RoomInfo.Stats.Kills++; killer.stats.Kills++; }
            if (ValidPlayer(target)) { target.RoomInfo.Stats.Deaths++; target.stats.Deaths++; }
            if (target == PlayerAlphaBattle && killer == PlayerBetaBattle)
            {
                Stats(killer).BattleWins++;
                Journal.Information("[ARENA] {b} killed {a} -> Beta wins round (streak {s})",
                    killer.Account.Nickname, target.Account.Nickname, Stats(killer).BattleWins);
                _roundWinner = Team.Beta;
                EnterAfterKill();
            }
            else if (target == PlayerBetaBattle && killer == PlayerAlphaBattle)
            {
                Stats(killer).BattleWins++;
                Journal.Information("[ARENA] {a} killed {b} -> Alpha wins round (streak {s})",
                    killer.Account.Nickname, target.Account.Nickname, Stats(killer).BattleWins);
                _roundWinner = Team.Alpha;
                EnterAfterKill();
            }
        }
        public override void OnScoreSuicide(Player plr, LongPeerId scorePlr, AttackAttribute icon)
        {
            if (_stage == RoundStage.AfterKill && _roundWinner != null && plr != null &&
                _stageElapsed < TimeSpan.FromSeconds(1.5))
            {
                var champFighter = _roundWinner == Team.Alpha ? PlayerAlphaBattle : PlayerBetaBattle;
                if (plr == champFighter)
                {
                    if (ValidPlayer(champFighter) && Stats(champFighter).BattleWins > 0)
                        Stats(champFighter).BattleWins--;
                    _roundWinner = null;
                    Journal.Information("[ARENA] Self-kill right after a kill (own bomb) -> draw");
                }
                return;
            }
            if (!ScoreIsPlaying() || _stage != RoundStage.Fighting || plr == null)
                return;
            if (plr == PlayerAlphaBattle)
            {
                Journal.Information("[ARENA] {a} self-killed -> Beta wins round", plr.Account.Nickname);
                _roundWinner = Team.Beta;
                EnterAfterKill();
            }
            else if (plr == PlayerBetaBattle)
            {
                Journal.Information("[ARENA] {b} self-killed -> Alpha wins round", plr.Account.Nickname);
                _roundWinner = Team.Alpha;
                EnterAfterKill();
            }
        }
        public void OnSetGameOption(Player plr, int option)
        {
            Trace($"<- {plr.Account.Nickname} Arena_Set_Game_Option_Req(2066) Unk={option}");
            Trace($"BROADCAST Arena_Set_Game_Option_Ack(3100) Unk={option}");
            Room.Broadcast(new ArenaSetGameOptionAckMessage(option));
        }
        public void OnSpecialPoint(Player plr, int unk1, int unk2)
        {
            Journal.Information("[ARENA] <- {nick} Arena_Special_Point_Req {u1},{u2}", plr.Account.Nickname, unk1, unk2);
            var team = plr.RoomInfo.Team;
            if (team == null || team.NoSpectatorPlayers.Count() <= 1)
            {
                Trace($"-> {plr.Account.Nickname} Arena_Special_Point_Ack(3101) AssistPoint=0 AccountId={plr.Account.Id} Point=0 (1v1, no charge)");
                plr.SendAsync(new ArenaSpecialPointAckMessage(0, plr.Account.Id, 0));
                return;
            }
            const uint SupportChargeRate = 2;
            if (unk1 == 1 || unk1 == 2)
            {
                _supportBarByTeam.TryGetValue(team.Team, out var have);
                var spend = (uint)(unk1 == 1 ? 50 : 100);
                var leftover = have > spend ? have - spend : 0u;
                _supportBarByTeam[team.Team] = leftover;
                Trace($"BROADCAST(team) Arena_Special_Point_Ack(3101) USE u1={unk1} -> Point={leftover}");
                foreach (var mate in team.NoSpectatorPlayers)
                    mate.SendAsync(new ArenaSpecialPointAckMessage(0, plr.Account.Id, leftover));
                return;
            }
            _supportBarByTeam.TryGetValue(team.Team, out var have2);
            var gain = unk2 > 0 ? (uint)unk2 * SupportChargeRate : 0u;
            var updated = Math.Max(have2, Math.Min(100u, have2 + gain));
            _supportBarByTeam[team.Team] = updated;
            Trace($"BROADCAST(team) Arena_Special_Point_Ack(3101) AssistPoint=0 AccountId={plr.Account.Id} Point={updated} (added {unk2})");
            foreach (var mate in team.NoSpectatorPlayers)
                mate.SendAsync(new ArenaSpecialPointAckMessage(0, plr.Account.Id, updated));
        }
        public void OnDrawHealthPoint(Player plr, ArenaDrawHealthPointAckMessage msg)
        {
            var hp = BitConverter.Int32BitsToSingle(msg.Unk4);
            Journal.Information("[ARENA] <- {nick} Arena_Draw_HealthPoint_Ack HP={hp} (raw {raw})",
                plr.Account.Nickname, hp, msg.Unk4);
            if (!_hpSnapshotSent || _stage != RoundStage.Fighting)
                return;
            _reportedHp[plr.Account.Id] = hp;
        }
        private void BroadcastBattleIndex()
        {
            foreach (var plr in Room.TeamManager.PlayersPlaying)
                SendBattleIndex(plr);
        }
        private void BroadcastFightersOnly()
        {
            var a = ValidPlayer(PlayerAlphaBattle)
                ? new[] { new ArenaSyncDto(0u, PlayerAlphaBattle.Account.Id) } : Array.Empty<ArenaSyncDto>();
            var b = ValidPlayer(PlayerBetaBattle)
                ? new[] { new ArenaSyncDto(0u, PlayerBetaBattle.Account.Id) } : Array.Empty<ArenaSyncDto>();

            var aIds = Room.TeamManager[Team.Alpha].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            var bIds = Room.TeamManager[Team.Beta].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            Trace($"BROADCAST fighter-only 3097 + roster 3098 alpha=[{string.Join(",", aIds)}] beta=[{string.Join(",", bIds)}]");
            foreach (var plr in Room.TeamManager.PlayersPlaying)
            {
                plr.SendAsync(new SyncFirstArenaBattleIdxMessage(aIds, bIds));
                plr.SendAsync(new SyncArenaBattleIdxMessage(CurrentRound, a, b));
            }
        }
        private void SendBattleIndex(Player plr)
        {
            var alphaDtos = BuildSyncs(Team.Alpha, PlayerAlphaBattle);
            var betaDtos = BuildSyncs(Team.Beta, PlayerBetaBattle);

            var alphaIds = Room.TeamManager[Team.Alpha].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            var betaIds = Room.TeamManager[Team.Beta].NoSpectatorPlayers.Select(x => x.Account.Id).ToArray();
            Trace($"-> {plr.Account.Nickname} Sync_First_Arena_Battle_Idx(3098) " +
                $"alpha=[{string.Join(",", alphaIds)}] beta=[{string.Join(",", betaIds)}]");
            Trace($"-> {plr.Account.Nickname} Sync_Arena_Battle_Idx(3097) round={CurrentRound} " +
                $"alpha=[{string.Join(",", alphaDtos.Select(d => $"(st={d.Status},id={d.AccountId})"))}] " +
                $"beta=[{string.Join(",", betaDtos.Select(d => $"(st={d.Status},id={d.AccountId})"))}]");
            plr.Session.SendAsync(new SyncFirstArenaBattleIdxMessage(alphaIds, betaIds));
            plr.Session.SendAsync(new SyncArenaBattleIdxMessage(CurrentRound, alphaDtos, betaDtos));
        }
        private static readonly object TraceLock = new object();
        private static void Trace(string text)
        {
            var line = "[ARENA-PKT] " + text;
            try
            {
                lock (TraceLock)
                    File.AppendAllText("arenapaquetes.log",
                        DateTime.Now.ToString("HH:mm:ss.fff") + " " + line + Environment.NewLine);
            }
            catch { }
        }
        private ArenaSyncDto[] BuildSyncs(Team team, Player current)
        {
            var roster = Room.TeamManager[team].PlayersPlaying;
            var fighter = ValidPlayer(current)
                ? current
                : roster.OrderByDescending(p => p.Level).FirstOrDefault();
            if (fighter == null)
                return Array.Empty<ArenaSyncDto>();

            return new[] { new ArenaSyncDto(0u, fighter.Account.Id) };
        }
        private bool CanStartGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;
            var teams = Room.TeamManager.Values.ToArray();
            if (teams.Any(team => team.NoSpectatorPlayers.Count() == 0))
                return false;
            if (Room.Options.IsFriendly)
                return true;
            return teams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }
    }
    internal class ArenaBriefing : Briefing
    {
        public ArenaBriefing(GameRuleBase gameRule)
            : base(gameRule)
        {
        }
        public override PlayerTeam GetWinnerTeam()
        {
            var standing = GameRule.Room.TeamManager.Values
                .Where(t => t.PlayersPlaying.Any())
                .ToArray();
            if (standing.Length == 1)
                return standing[0];
            return base.GetWinnerTeam();
        }
        protected override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
        }
    }
    internal class ArenaPlayerRecord : PlayerRecord
    {
        public ArenaPlayerRecord(Player plr)
            : base(plr)
        {
        }
        public override uint TotalScore => 5 * RoundsWon + 2 * Kills + KillAssists + HealAssists;
        public uint HealAssists { get; set; }
        public uint RoundsWon { get; set; }
        public uint BattleWins { get; set; }
        public uint SpecialBarScore { get; set; }
        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
            var teams = Player.Room.TeamManager;
            var alphaScore = teams[Team.Alpha]?.Score ?? 0;
            var betaScore = teams[Team.Beta]?.Score ?? 0;
            w.Write(alphaScore);
            w.Write(betaScore);
            w.Write(betaScore);
            w.Write(alphaScore);
            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(Deaths);
            w.Write(HealAssists);
            w.Write(RoundsWon);
            w.Write(0);
            w.Write((byte)0);
            w.Write(0);
        }
        public override void Reset()
        {
            base.Reset();
            HealAssists = 0;
            RoundsWon = 0;
            BattleWins = 0;
            SpecialBarScore = 0;
        }
    }
}

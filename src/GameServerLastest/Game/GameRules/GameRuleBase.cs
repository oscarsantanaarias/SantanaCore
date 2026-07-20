using Santana.Network;
namespace Santana.Game.GameRules
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using MySqlConnector;
    using Santana;
    using Santana.Database.Game;
    using Santana.Network.Data.Game;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using Santana.Network.Services;
    using Serilog;
    using Serilog.Core;
    using Stateless;
    internal abstract class GameRuleBase
    {
        private static readonly TimeSpan CoinSpawnInterval = TimeSpan.FromSeconds(45);
        private static readonly TimeSpan HalfTimeCountdownLength = TimeSpan.FromSeconds(9);
        private static readonly TimeSpan ResultCountdownLength = TimeSpan.FromSeconds(9);
        private static readonly TimeSpan HalfTimePauseLength = TimeSpan.FromSeconds(25);
        private static readonly TimeSpan ResultScreenLength = TimeSpan.FromSeconds(14);
        private static readonly TimeSpan LoadingDeadline = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan StartCountdownLength = TimeSpan.FromMilliseconds(3500);
        private TimeSpan _coinTimer;
        private static readonly ILogger Log0 =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(GameRuleBase));
        protected GameRuleBase(Room room)
        {
            Room = room;
            StateMachine = new StateMachine<GameRuleState, GameRuleStateTrigger>(GameRuleState.Waiting);
            StateMachine.OnTransitioned(HandleTransition);
            Reload();
        }
        public abstract GameRule GameRule { get; }
        public abstract bool CountMatch { get; }
        public Room Room { get; }
        public abstract Briefing Briefing { get; }
        public StateMachine<GameRuleState, GameRuleStateTrigger> StateMachine { get; }
        public TimeSpan RoundTime { get; private set; }
        public TimeSpan GameStartTime { get; set; }
        public GameStartState PrepareState { get; set; }
        public virtual bool BlockPlaying => false;
        public virtual void Initialize()
        {
        }
        public virtual void ResetAfterSurrender()
        {
        }
        public virtual void Cleanup()
        {
        }
        public virtual void Reload()
        {
        }
        public virtual void OnRoomJoinCompleted(Player plr)
        {
        }
        public virtual void OnIntrudeCompleted(Player plr)
        {
        }
        public virtual void OnPlayerLeaving(Player plr)
        {
        }
        public virtual void OnBeforeIntrudeSpawn(Player plr)
        {
        }
        public void RoomJoinCompleted(Player plr)
        {
            OnRoomJoinCompleted(plr);
        }
        public void IntrudeCompleted(Player plr)
        {
            OnIntrudeCompleted(plr);
        }
        public virtual void UpdateTime(Player plr)
        {
            plr?.Session?.SendAsync(new GameRefreshGameRuleInfoAckMessage(Room.GameState, Room.SubGameState,
                Room.RoundTime));
        }
        public void UpdateTime(TimeSpan elapsed = default(TimeSpan))
        {
            var stamp = elapsed != default(TimeSpan) ? elapsed : Room.RoundTime;
            foreach (var loaded in Room.Players.Values.Where(x => x.RoomInfo.HasLoaded))
            {
                loaded.Session.SendAsync(new GameRefreshGameRuleInfoAckMessage(Room.GameState, Room.SubGameState, stamp));
            }
        }
        public void EventCoinDrop(TimeSpan delta)
        {
            _coinTimer += delta;
            if (_coinTimer < CoinSpawnInterval)
                return;
            var roll = new Random().Next(100, 50000);
            Room.Broadcast(new Network.Message.Game.PromotionCoinEventDropCoinAckMessage { Ammo = 10, Unk = 1000, Posions = (uint)roll });
            _coinTimer = TimeSpan.Zero;
        }
        public virtual void Update(TimeSpan delta)
        {
            RoundTime += delta;
            Room.RoundTime = RoundTime;
            #region PrepareGame
            if (StateMachine.IsInState(GameRuleState.Preparing))
            {
                switch (PrepareState)
                {
                    case GameStartState.Loading:
                        if (Room.RoundTime > LoadingDeadline)
                        {
                            foreach (var stuck in Room.Players.Values.Where(x =>
                                (x.RoomInfo.IsReady || Room.Master == x) && !x.RoomInfo.HasLoaded))
                            {
                                stuck.RoomInfo.IsReady = false;
                                stuck.Room?.Leave(stuck);
                            }
                        }
                        if (Room.Players.Values.Count(x => x.RoomInfo.HasLoaded) >=
                            Room.Players.Values.Count(x => x.RoomInfo.IsReady || Room.Master == x) ||
                            Room.RoundTime > LoadingDeadline)
                        {
                            GameStartTime = Room.RoundTime;
                            if (GameRule == GameRule.Chaser ||
                                GameRule == GameRule.Practice ||
                                GameRule == GameRule.CombatTrainingDM ||
                                GameRule == GameRule.CombatTrainingTD)
                            {
                                PrepareState = GameStartState.ReadyToStart;
                            }
                            else
                            {
                                PrepareState = GameStartState.Countdown;
                                foreach (var ready in Room.Players.Values)
                                {
                                    if (ready.RoomInfo.HasLoaded)
                                    {
                                        ready.Session.SendAsync(
                                            new RoomGamePlayCountDownAckMessage(
                                                StartCountdownLength));
                                        ready.Session.SendAsync(new GameRefreshGameRuleInfoAckMessage(Room.GameState,
                                            GameTimeState.StartGameCounter,
                                            Room.Options.TimeLimit));
                                    }
                                }
                            }
                        }
                        break;
                    case GameStartState.Countdown:
                        if ((Room.RoundTime - GameStartTime).TotalMilliseconds >
                            StartCountdownLength.TotalMilliseconds + 500)
                            PrepareState = GameStartState.ReadyToStart;
                        break;
                    case GameStartState.ReadyToStart:
                        if (StateMachine.CanFire(GameRuleStateTrigger.StartGame))
                        {
                            Room.IsPreparing = false;
                            RoundTime = TimeSpan.Zero;
                            PrepareState = GameStartState.Playing;
                            StateMachine.Fire(GameRuleStateTrigger.StartGame);
                        }
                        break;
                }
            }
            #endregion
            #region Playing
            if (StateMachine.IsInState(GameRuleState.Playing))
            {
                foreach (var active in Room.TeamManager.PlayersPlaying)
                {
                    EventCoinDrop(delta);
                    active.RoomInfo.PlayTime += delta;
                    active.RoomInfo.CharacterPlayTime[active.CharacterManager.CurrentSlot] += delta;
                    if (active.Room.Options.Ping > 0 && active.Room.Options.Ping < 1000)
                    {
                    }
                }
            }
            #endregion
            #region HalfTime
            if (StateMachine.IsInState(GameRuleState.EnteringHalfTime))
            {
                if (RoundTime >= HalfTimeCountdownLength)
                {
                    if (StateMachine.CanFire(GameRuleStateTrigger.StartHalfTime))
                    {
                        RoundTime = TimeSpan.Zero;
                        StateMachine.Fire(GameRuleStateTrigger.StartHalfTime);
                    }
                }
                else
                {
                    foreach (var active in Room.TeamManager.PlayersPlaying)
                    {
                        var secs = ((int)(HalfTimeCountdownLength - RoundTime).TotalSeconds + 1).ToString();
                        active.Session.SendAsync(new GameEventMessageAckMessage(GameEventMessage.HalfTimeIn, 2, 0, 0,
                            secs));
                    }
                }
            }
            if (StateMachine.IsInState(GameRuleState.HalfTime) &&
                RoundTime >= HalfTimePauseLength)
            {
                StateMachine.Fire(GameRuleStateTrigger.StartSecondHalf);
            }
            #endregion
            #region Result
            if (StateMachine.IsInState(GameRuleState.EnteringResult))
            {
                if (RoundTime >= ResultCountdownLength)
                {
                    RoundTime = TimeSpan.Zero;
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);
                }
                else
                {
                    foreach (var active in Room.TeamManager.PlayersPlaying)
                    {
                        var secs = (int)(ResultCountdownLength - RoundTime).TotalSeconds + 1 + " second(s)";
                        active.Session?.SendAsync(new GameEventMessageAckMessage(GameEventMessage.ResultIn, 3, 0, 0,
                            secs));
                    }
                }
            }
            if (StateMachine.IsInState(GameRuleState.Result) &&
                RoundTime >= ResultScreenLength)
            {
                if (StateMachine.CanFire(GameRuleStateTrigger.EndGame))
                {
                    RoundTime = TimeSpan.Zero;
                    StateMachine.Fire(GameRuleStateTrigger.EndGame);
                }
            }
            #endregion
            #region CoinBuff
            if (this.StateMachine.IsInState(GameRuleState.Playing))
            {
                try
                {
                    foreach (var active in this.Room.TeamManager.PlayersPlaying)
                    {
                        active.PlayerCoinBuff.Update(1);
                    }
                }
                catch { }
            }
            #endregion
        }
        public abstract PlayerRecord GetPlayerRecord(Player plr);
        private void ApplyHpMpOptions(Player plr)
        {
            if (plr == null)
                return;
            if ((plr.Room.Options.HP > 0 && plr.Room.Options.HP < 100) || plr.Room.Options.HP > 100)
                plr.SendAsync(new AdminChangeHPAckMessage { Value = plr.Room.Options.HP + plr.CharacterManager.CurrentCharacter.GetHP() });
            if ((plr.Room.Options.MP > 0 && plr.Room.Options.MP < 100) || plr.Room.Options.MP > 100)
                plr.SendAsync(new AdminChangeMPAckMessage { Value = plr.Room.Options.MP + plr.CharacterManager.CurrentCharacter.GetSP() });
        }
        private void HandleTransition(
            StateMachine<GameRuleState, GameRuleStateTrigger>.Transition transition)
        {
            RoundTime = TimeSpan.Zero;
            try
            {
                switch (transition.Trigger)
                {
                    case GameRuleStateTrigger.StartPrepare:
                        Room.IsPreparing = true;
                        foreach (var participant in Room.TeamManager.Players.Where(plr =>
                            plr.RoomInfo.IsReady || Room.Master == plr ||
                            plr.RoomInfo.Mode == PlayerGameMode.Spectate))
                        {
                            participant.Session.SendAsync(new RoomGameLoadingAckMessage());
                            participant.Session.SendAsync(new RoomBeginRoundAckMessage());
                            participant.RoomInfo.State = PlayerState.Waiting;
                        }
                        PrepareState = GameStartState.Loading;
                        Room.GameState = GameState.Loading;
                        Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                        return;
                }
                switch (transition.Destination)
                {
                    case GameRuleState.FullGame:
                        Room.HasStarted = true;
                        Room.GameState = GameState.Playing;
                        foreach (var team in Room.TeamManager.Values)
                            team.Score = 0;
                        foreach (var participant in Room.TeamManager.PlayersPlaying)
                        {
                            participant.Session.SendAsync(new RoomGameStartAckMessage());
                            participant.RoomInfo.State = participant.RoomInfo.Mode == PlayerGameMode.Normal
                                 ? PlayerState.Alive : PlayerState.Spectating;
                            ApplyHpMpOptions(participant);
                        }
                        Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                        break;
                    case GameRuleState.FirstHalf:
                        Room.HasStarted = true;
                        Room.GameState = GameState.Playing;
                        Room.SubGameState = GameTimeState.FirstHalf;
                        UpdateTime(TimeSpan.FromMilliseconds(-5));
                        foreach (var team in Room.TeamManager.Values)
                            team.Score = 0;
                        foreach (var participant in Room.TeamManager.PlayersPlaying)
                        {
                            participant.Session.SendAsync(new RoomGameStartAckMessage());
                            participant.RoomInfo.State = participant.RoomInfo.Mode == PlayerGameMode.Normal
                                 ? PlayerState.Alive : PlayerState.Spectating;
                            ApplyHpMpOptions(participant);
                        }
                        Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                        break;
                    case GameRuleState.HalfTime:
                        foreach (var participant in Room.TeamManager.PlayersPlaying)
                        {
                            participant.RoomInfo.State = PlayerState.Waiting;
                        }
                        Room.SubGameState = GameTimeState.HalfTime;
                        Room.Broadcast(new GameChangeSubStateAckMessage(Room.SubGameState));
                        break;
                    case GameRuleState.SecondHalf:
                        foreach (var participant in Room.TeamManager.PlayersPlaying)
                        {
                            participant.RoomInfo.State = participant.RoomInfo.Mode == PlayerGameMode.Normal
                                 ? PlayerState.Alive : PlayerState.Spectating;
                            ApplyHpMpOptions(participant);
                        }
                        Room.SubGameState = GameTimeState.SecondHalf;
                        Room.Broadcast(new GameChangeSubStateAckMessage(Room.SubGameState));
                        break;
                    case GameRuleState.Result:
                        var winnerList = new List<Player>();
                        var byScore = Room.TeamManager.PlayersPlaying.OrderBy(x => x.RoomInfo.Stats.TotalScore).ToList();
                        foreach (var winner in Room.GameRuleManager.GameRule.Briefing.GetWinnerTeam().Keys)
                        {
                            if (CountMatch)
                            {
                                var picker = new SecureRandom();
                                var pick = picker.Next(0, 9);
                                var cardItems = new ItemNumber[] { 8020001, 8020002, 8020003, 8020004, 8020005, 8020006,
                                     8020007, 8020008, 8020009, 8020010};
                                var cardLabels = new string[] { "S4", "S", "4", "L", "E", "A", "G", "U", "E", "Fumbi" };
                                winner.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, winner.Account.Id, "CardSystem", $"You received Card {cardLabels[pick]}"));
                                winner.Inventory.CreateUnits(cardItems[pick], 1);
                                winner.DailyMission.DailyMiison();
                                winner.AchieveMission.AchieveMissionInfo();
                                winner.stats.Won++;
                                winner.AddCombiMatchStats(true);
                                if (byScore.FindIndex(x => x == winner) >= 3) ;
                            }
                            winnerList.Add(winner);
                        }
                        if (CountMatch)
                        {
                            foreach (var loser in Room.TeamManager.PlayersPlaying)
                            {
                                if (!winnerList.Contains(loser))
                                {
                                    var picker = new SecureRandom();
                                    var pick = picker.Next(0, 10);
                                    var cardItems = new ItemNumber[] { 8020000, 8020001, 8020002, 8020003, 8020004, 8020005, 8020006,
                                     8020007, 8020008, 8020009, 8020010};
                                    var cardLabels = new string[] { "S4", "S", "4", "L", "E", "A", "G", "U", "E", "Fumbi" };
                                    loser.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, loser.Account.Id, "System", $"You received Card {cardLabels[pick]}"));
                                    loser.Inventory.CreateUnits(cardItems[pick], 1);
                                    loser.DailyMission.DailyMiison();
                                    loser.AchieveMission.AchieveMissionInfo();
                                    if (Room.GameRuleManager.GameRule.GameRule == GameRule.Chaser)
                                    {
                                        loser.stats.Won++;
                                        loser.AddCombiMatchStats(true);
                                    }
                                    if (!Room.HasSurrender && Room.GameRuleManager.GameRule.GameRule != GameRule.Chaser)
                                    {
                                        loser.stats.Loss++;
                                        loser.AddCombiMatchStats(false);
                                    }
                                }
                            }
                        }
                        Room.HasStarted = false;
                        Room.GameState = GameState.Result;
                        Room.SubGameState = GameTimeState.None;
                        Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                        Room.BroadcastBriefing(true);
                        break;
                    case GameRuleState.Waiting:
                        foreach (var team in Room.TeamManager.Values)
                            team.Score = 0;
                        foreach (var participant in Room.TeamManager.Players)
                        {
                            participant.Room.HasSurrender = false;
                            participant.RoomInfo.Reset();
                            participant.RoomInfo.State = PlayerState.Lobby;
                            OnRoomJoinCompleted(participant);
                        }
                        ResetAfterSurrender();
                        Reload();
                        Room.HasStarted = false;
                        Room.GameState = GameState.Waiting;
                        Room.SubGameState = GameTimeState.None;
                        Room.Broadcast(new GameChangeStateAckMessage(Room.GameState));
                        PrepareState = GameStartState.Waiting;
                        Room.BroadcastBriefing();
                        break;
                }
            }
            catch (Exception e)
            {
                Log0.Error(e.ToString());
            }
        }
        #region Scores
        public virtual void Respawn(Player victim)
        {
            if (victim == null)
                return;
            victim.RoomInfo.State = PlayerState.Dead;
            victim.Session.SendAsync(new InGamePlayerResponseOfDeathAckMessage());
        }
        public virtual void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            if (killer == null || target == null)
                return;
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
            if (killerIsReal)
            {
                killer.RoomInfo.Stats.Kills++;
                killer.stats.Kills++;
            }
            if (targetIsReal)
            {
                target.RoomInfo.Stats.Deaths++;
                target.stats.Deaths++;
            }
            if (assistIsReal)
            {
                assist.AchieveMission.KillsAssist++;
                assist.RoomInfo.Stats.KillAssists++;
                assist.stats.KillAssists++;
                Room.Broadcast(new ScoreKillAssistAckMessage(new ScoreAssistDto(scoreKiller, scoreAssist, scoreTarget,
                    attackAttribute)));
                if (Room.Options.Heal / 2 > 0.0f)
                {
                }
            }
            else
            {
                killer.AchieveMission.Kills++;
                killer.DailyMission.Kills++;
                Room.Broadcast(new ScoreKillAckMessage(new ScoreDto(scoreKiller, scoreTarget, attackAttribute)));
                if (Room.Options.Heal > 0.0f)
                {
                }
            }
            if (killer.PlayerCoinBuff.FindBuff(BuffType.PEN).IsEnabled)
            {
                killer.LuckyShot.TryShot(LuckyShotType.PEN);
            }
            if (killer.PlayerCoinBuff.FindBuff(BuffType.EXP).IsEnabled)
            {
                killer.LuckyShot.TryShot(LuckyShotType.EXP);
            }
            if (killer.Room.Players.Where(x => x.Value.RoomInfo.Stats.Kills == 0).Count() == killer.Room.Players.Count() - 1)
            {
                killer.RoomInfo.Stats.FirstKill = true;
            }
        }
        public virtual void OnScoreTeamKill(Player killer, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreKiller, LongPeerId scoreTarget)
        {
            var targetIsReal = (target?.RoomInfo.PeerId.EqualSlot(scoreTarget) ?? false) &&
                                   target.RoomInfo.PeerId.IsPlayer();
            if (targetIsReal)
                Respawn(target);
            if (!ScoreIsPlaying())
                return;
            if (targetIsReal)
            {
                Respawn(target);
                target.RoomInfo.Stats.Deaths++;
                target.stats.Deaths++;
            }
            Room.Broadcast(new ScoreTeamKillAckMessage(new Score2Dto(scoreKiller, scoreTarget, attackAttribute)));
        }
        public virtual void OnScoreHeal(Player plr, LongPeerId scorePlr)
        {
            if (!ScoreIsPlaying())
                return;
            var healerIsReal = (plr?.RoomInfo.PeerId.EqualSlot(scorePlr) ?? false) && plr.RoomInfo.PeerId.IsPlayer();
            if (healerIsReal)
            {
                plr.stats.Heal++;
            }
            Room.Broadcast(new ScoreHealAssistAckMessage(scorePlr));
        }
        public virtual void OnScoreSuicide(Player plr, LongPeerId scorePlr, AttackAttribute icon)
        {
            var suiciderIsReal = (plr?.RoomInfo.PeerId.EqualSlot(scorePlr) ?? false) && plr.RoomInfo.PeerId.IsPlayer();
            if (suiciderIsReal)
                Respawn(plr);
            if (!ScoreIsPlaying())
                return;
            if (suiciderIsReal)
            {
                plr.RoomInfo.Stats.Deaths++;
                plr.stats.Deaths++;
            }
            Room.Broadcast(new ScoreSuicideAckMessage(scorePlr, icon));
        }
        #endregion
        public bool ScoreIsPlaying()
        {
            var inPlayState = StateMachine.IsInState(GameRuleState.FirstHalf) ||
                              StateMachine.IsInState(GameRuleState.SecondHalf) ||
                              StateMachine.IsInState(GameRuleState.FullGame);
            return inPlayState && !BlockPlaying;
        }
    }
}

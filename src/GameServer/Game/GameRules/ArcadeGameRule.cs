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

    internal class ArcadeGameRule : GameRuleBase
    {
        public ArcadeGameRule(Room room)
            : base(room)
        {
            Briefing = new ArcadeBriefing(this);

            StateMachine.Configure(GameRuleState.Waiting)
                .OnEntry(SendStageInfoToRoom)
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
                .OnEntry(SendArcadeResult)
                .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting);
        }

        public static void SendArcadeRefresh(Player plr)
        {
            plr.SendAsync(new Santana.Network.Message.Game.PlayeArcadeMapInfoAckMessage());
            plr.SendAsync(BuildStageInfoAck(plr));
        }

        public static Santana.Network.Message.Game.PlayerArcadeStageInfoAckMessage BuildStageInfoAck(Player plr)
        {
            var stats = plr.stats.GetArcadeStats();
            return new Santana.Network.Message.Game.PlayerArcadeStageInfoAckMessage
            {
                Infos = (from stage in Enumerable.Range(1, 8)
                         from mode in Enumerable.Range(0, 4)
                         select new Santana.Network.Data.Game.ArcadeStageInfoDto
                         {
                             Unk1 = 50,
                             Unk2 = (uint)stage,
                             Unk3 = (uint)mode,
                             Unk13 = (byte)(stats.IsStageCleared((byte)stage) ? 1 : 0)
                         }).ToArray()
            };
        }

        private void SendStageInfoToRoom()
        {
            foreach (var plr in Room.TeamManager.Players)
                SendArcadeRefresh(plr);
        }

        private void SendArcadeResult()
        {
            var players = Room.TeamManager.Players.ToList();
            foreach (var receiver in players)
                SendArcadeRefresh(receiver);
            foreach (var receiver in players)
            {
                using (var ms = new MemoryStream())
                using (var w = new BinaryWriter(ms))
                {
                    foreach (var plr in players)
                    {
                        var rec = plr.RoomInfo.Stats as ArcadePlayerRecord;
                        var isMe = plr.Account.Id == receiver.Account.Id;
                        w.Write(isMe ? (ulong)1 : (ulong)plr.Account.Id);
                        w.Write((int)(rec?.KilledMonster ?? 0));
                        w.Write(Math.Min(100, Math.Max(0, plr.RoomInfo.ArcadeRespawnCount * 10)));
                        w.Write((int)plr.RoomInfo.PlayTime.TotalSeconds);
                        w.Write(isMe ? 1 : 0);
                        w.Write(0);
                        w.Write(0);
                    }
                    receiver.SendAsync(new ArcadeStageBriefingAckMessage { Unk1 = 0, Unk2 = 0, Data = ms.ToArray() });
                }
            }
        }

        private static readonly ConcurrentDictionary<ulong, ArcadeScoreSyncReqDto> _scoreByAccount = new ConcurrentDictionary<ulong, ArcadeScoreSyncReqDto>();
        private byte _stage = 1;
        private int _scoreCheck = 0;
        private readonly System.Collections.Generic.HashSet<ulong> _failedPlayers = new System.Collections.Generic.HashSet<ulong>();
        private readonly System.Collections.Generic.HashSet<ulong> _downed = new System.Collections.Generic.HashSet<ulong>();

        public override GameRule GameRule => GameRule.Arcade;

        public override Briefing Briefing { get; }

        public override bool CountMatch => true;

        public ArcadeBriefing GetBriefing()
        {
            return (ArcadeBriefing)Briefing;
        }

        public override void Initialize()
        {
            var maxPlayers = Math.Max(1u, (uint)Room.Options.PlayerLimit);
            var maxSpectators = (uint)Room.Options.SpectatorLimit;

            Room.TeamManager.Add(Team.Alpha, maxPlayers, maxSpectators);
            base.Initialize();
        }

        public override void Cleanup()
        {
            Room.TeamManager.Remove(Team.Alpha);
            base.Cleanup();
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
                if (Room.GameState != GameState.Playing ||
                    StateMachine.IsInState(GameRuleState.EnteringResult) ||
                    StateMachine.IsInState(GameRuleState.Result) ||
                    RoundTime < TimeSpan.FromSeconds(5))
                    return;

                var timeCap = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds);
                if (RoundTime >= timeCap)
                    StateMachine.Fire(GameRuleStateTrigger.StartResult);
            }
            catch (Exception ex)
            {
                Room.Logger.Error(ex.ToString());
            }
        }

        public void ArcadeStageBegin(GameSession session, byte unk)
        {
            var plr = session.Player;

            Console.WriteLine("Arcade: a client asked to begin the stage");
            Console.WriteLine($"Arcade right now: State={StateMachine.State}, CanStart={StateMachine.CanFire(GameRuleStateTrigger.StartPrepare)}, Players={Room.TeamManager.NoSpectatorPlayers.Count()}");

            plr.Room.Broadcast(new RoomGameEndLoadingAckMessage(plr.Account.Id));

            if (StateMachine.CanFire(GameRuleStateTrigger.StartPrepare))
                StateMachine.Fire(GameRuleStateTrigger.StartPrepare);

            plr.Room.Broadcast(new ArcadeBeginRoundAckMessage
            {
                Unk1 = (byte)Math.Max(1, Room.TeamManager.NoSpectatorPlayers.Count()),
                Unk2 = _stage,
                Unk3 = 0x0A
            });

            foreach (var p in Room.TeamManager.NoSpectatorPlayers)
                p.RoomInfo.ArcadeRespawnCount = 10;
        }

        public void ArcadeStageSelect(GameSession session, byte stage, byte unk)
        {
            _stage = stage;
            session.SendAsync(new ArcadeStageSelectAckMessage { Unk1 = stage, Unk2 = unk });
        }

        public void ArcadeStageClear(ArcadeScoreSyncDto[] score)
        {
            foreach (var scoreItem in score)
                _scoreCheck += scoreItem.KilledMonster;

            foreach (var plr in Room.TeamManager.PlayersPlaying)
            {
                plr.stats.GetArcadeStats().MarkStageCleared(_stage);
                SendArcadeRefresh(plr);
            }

            if (StateMachine.CanFire(GameRuleStateTrigger.StartResult))
                Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }

        public void OnPlayerFailed(Player plr)
        {
            if (plr != null)
                _failedPlayers.Add(plr.Account.Id);
            var playing = Room.TeamManager.PlayersPlaying.Count();
            Console.WriteLine($"[ARCADE-FAIL] {plr?.Account.Nickname} failed={_failedPlayers.Count} playing={playing}");
            if (_failedPlayers.Count < System.Math.Max(1, playing))
                return;
            if (StateMachine.CanFire(GameRuleStateTrigger.StartResult))
                Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }

        public bool RequestRevive(Player plr)
        {
            _downed.Add(plr.Account.Id);

            var others = Room.TeamManager.PlayersPlaying
                .Where(p => p.Account.Id != plr.Account.Id)
                .ToList();

            if (others.Count == 0)
                return true;

            var aliveTeammate = others.Any(p =>
                !_downed.Contains(p.Account.Id) &&
                !_failedPlayers.Contains(p.Account.Id));
            if (aliveTeammate)
                return true;

            foreach (var p in Room.TeamManager.PlayersPlaying)
                _failedPlayers.Add(p.Account.Id);
            if (StateMachine.CanFire(GameRuleStateTrigger.StartResult))
                Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
            return false;
        }

        public void MarkRevived(Player plr)
        {
            _downed.Remove(plr.Account.Id);
        }

        public void OnArcadeScore(Player plr, ArcadeScoreSyncDto[] score)
        {
            var ownScore = score.Where(x => x.AccountId == plr.Account.Id).FirstOrDefault();
            if (ownScore == null)
                return;

            var synced = new ArcadeScoreSyncReqDto();

            synced.AccountId = plr.Account.Id;
            synced.Unk1 = ownScore.MonsterCount;
            synced.Unk2 = ownScore.MaxMonster;
            synced.Unk3 = ownScore.KilledMonster;
            var totalKilled = score.Sum(x => x.KilledMonster);
            synced.Unk4 = totalKilled > 0 ? System.Math.Max(0, System.Math.Min(100, (int)(0.5f + (100f * ownScore.KilledMonster / totalKilled)))) : 0;

            GetRecord(plr).KilledMonster = (uint)ownScore.KilledMonster;

            if (_scoreByAccount.ContainsKey(plr.Account.Id))
            {
                _scoreByAccount.TryUpdate(plr.Account.Id, synced, _scoreByAccount[plr.Account.Id]);
            }
            else
            {
                _scoreByAccount.TryAdd(plr.Account.Id, synced);
            }

            Room?.Broadcast(new ArcadeScoreSyncAckMessage(_scoreByAccount.Values.ToArray()));
        }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new ArcadePlayerRecord(plr);
        }

        private static ArcadePlayerRecord GetRecord(Player plr)
        {
            return (ArcadePlayerRecord)plr.RoomInfo.Stats;
        }

        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);

            if (!ScoreIsPlaying())
                return;
        }

        public override void OnScoreSuicide(Player target, LongPeerId scoreTarget, AttackAttribute icon)
        {
            base.OnScoreSuicide(target, scoreTarget, icon);

            if (!ScoreIsPlaying())
                return;
        }

        private bool CanStartGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;

            return Room.TeamManager.NoSpectatorPlayers.Count() >= 1;
        }
    }

    internal class ArcadeBriefing : Briefing
    {
        public ArcadeBriefing(GameRuleBase ruleBase)
            : base(ruleBase)
        {
        }
    }

    internal class ArcadePlayerRecord : PlayerRecord
    {
        public ArcadePlayerRecord(Player plr)
            : base(plr)
        {
        }

        public override uint TotalScore => 5 * QueenKills + BonusKillAssists + KilledMonster;
        public uint QueenKills { get; set; }
        public uint BonusKillAssists { get; set; }
        public uint KilledMonster { get; set; }

        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
            w.Write(Math.Min(100, Math.Max(0, Player.RoomInfo.ArcadeRespawnCount * 10)));
            w.Write((int)KilledMonster);
            w.Write((int)Player.RoomInfo.PlayTime.TotalSeconds);
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

            return (int)(placementBonus +
                          contenders.Length * expRates.PlayerCountFactor +
                          Player.RoomInfo.PlayTime.TotalMinutes * expRates.ExpPerMin);
        }
    }
}

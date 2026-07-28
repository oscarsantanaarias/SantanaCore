using System.Data;
using System.Linq;
using Dapper.FastCrud;
using Santana.Database.Game;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Game;
using Santana.Game.GameRules;
using System;
namespace Santana
{
    internal class StatsManager
    {
        private bool _isFriendly;
        private readonly Player _owner;
        private BaseStats _active;
        public StatsManager(Player player, PlayerDto playerDto)
        {
            _owner = player;
            DeathMatch = new DMStats(_owner, playerDto);
            TouchDown = new TDStats(_owner, playerDto);
            Chaser = new ChaserStats(_owner, playerDto);
            BattleRoyal = new BRStats(_owner, playerDto);
            Captain = new CPTStats(_owner, playerDto);
            Siege = new SiegeStats(_owner, playerDto);
            Arena = new ArenaStats(_owner, playerDto);
        }
        public DMStats DeathMatch { get; }
        public TDStats TouchDown { get; }
        public ChaserStats Chaser { get; }
        public BRStats BattleRoyal { get; }
        public CPTStats Captain { get; }
        public SiegeStats Siege { get; }
        public ArenaStats Arena { get; }
        public ulong Won
        {
            get => _active?.Won ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _owner.TotalWins++;
                _active.Won = value;
            }
        }
        public ulong Loss
        {
            get => _active?.Loss ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _owner.TotalLosses++;
                _active.Loss = value;
            }
        }
        public ulong Kills
        {
            get => _active?.Kills ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _active.Kills = value;
            }
        }
        public ulong KillAssists
        {
            get => _active?.KillAssists ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _active.KillAssists = value;
            }
        }
        public ulong Deaths
        {
            get => _active?.Deaths ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _active.Deaths = value;
            }
        }
        public ulong Heal
        {
            get => _active?.Heal ?? 0;
            set
            {
                if (_active == null || _isFriendly)
                    return;
                _active.Heal = value;
            }
        }
        public void OnJoin(GameRuleBase game)
        {
            _active = null;
            _isFriendly = !game.CountMatch;
            if (_isFriendly)
            {
                switch (game.GameRule)
                {
                    case GameRule.BattleRoyal:
                        _active = new BRStats(_owner);
                        break;
                    case GameRule.Captain:
                        _active = new CPTStats(_owner);
                        break;
                    case GameRule.Chaser:
                        _active = new ChaserStats(_owner);
                        break;
                    case GameRule.Deathmatch:
                        _active = new DMStats(_owner);
                        break;
                    case GameRule.Touchdown:
                        _active = new TDStats(_owner);
                        break;
                    case GameRule.Siege:
                        _active = new SiegeStats(_owner);
                        break;
                    case GameRule.Arena:
                        _active = new ArenaStats(_owner);
                        break;
                }
            }
            else
                switch (game.GameRule)
                {
                    case GameRule.BattleRoyal:
                        _active = BattleRoyal;
                        break;
                    case GameRule.Captain:
                        _active = Captain;
                        break;
                    case GameRule.Chaser:
                        _active = Chaser;
                        break;
                    case GameRule.Deathmatch:
                        _active = DeathMatch;
                        break;
                    case GameRule.Touchdown:
                        _active = TouchDown;
                        break;
                    case GameRule.Siege:
                        _active = Siege;
                        break;
                    case GameRule.Arena:
                        _active = Arena;
                        break;
                }
        }
        public DMStats GetDMStats()
        {
            return DeathMatch;
        }
        public TDStats GetTDStats()
        {
            return TouchDown;
        }
        public ChaserStats GetChaserStats()
        {
            return Chaser;
        }
        public BRStats GetBRStats()
        {
            return BattleRoyal;
        }
        public CPTStats GetCPTStats()
        {
            return Captain;
        }
        public SiegeStats GetSiegeStats()
        {
            return Siege;
        }
        public ArenaStats GetArenaStats()
        {
            return Arena;
        }
        public void Save(IDbConnection db)
        {
            DeathMatch.Save(db);
            TouchDown.Save(db);
            Chaser.Save(db);
            BattleRoyal.Save(db);
            Captain.Save(db);
            Siege.Save(db);
            Arena.Save(db);
        }
    }
    internal abstract class BaseStats
    {
        protected ulong _deaths;
        protected bool _existsInDatabase;
        protected ulong _heal;
        protected ulong _killAssists;
        protected ulong _kills;
        protected ulong _loss;
        protected bool _needsSave;
        protected ulong _won;
        public BaseStats(Player player)
        {
            Player = player;
        }
        public Player Player { get; set; }
        public ulong Won
        {
            get => _won;
            set
            {
                if (_won == value)
                    return;
                _won = value;
                _needsSave = true;
            }
        }
        public ulong Loss
        {
            get => _loss;
            set
            {
                if (_loss == value)
                    return;
                _loss = value;
                _needsSave = true;
            }
        }
        public ulong Kills
        {
            get => _kills;
            set
            {
                if (_kills == value)
                    return;
                _needsSave = true;
                _kills = value;
            }
        }
        public ulong KillAssists
        {
            get => _killAssists;
            set
            {
                if (_killAssists == value)
                    return;
                _needsSave = true;
                _killAssists = value;
            }
        }
        public ulong Deaths
        {
            get => _deaths;
            set
            {
                if (_deaths == value)
                    return;
                _needsSave = true;
                _deaths = value;
            }
        }
        public ulong Heal
        {
            get => _heal;
            set
            {
                if (_heal == value)
                    return;
                _heal = value;
                _needsSave = true;
            }
        }
        public float WinRate => _won + _loss > 0 ? _won / (float)(_won + _loss) : 1.0f;
        protected float TotalMatches => Won + Loss;
        protected static float Round1(float value)
        {
            return (float)Math.Round(value, 1, MidpointRounding.AwayFromZero);
        }
        protected static uint ToUInt32(ulong value)
        {
            return value > uint.MaxValue ? uint.MaxValue : (uint)value;
        }
        protected float AveragePerMatch(float value, float defaultValue = 0.5f)
        {
            return TotalMatches > 0 ? Round1(value / TotalMatches) : defaultValue;
        }
        protected float WinPercent => Round1(WinRate * 100.0f);
        public abstract void Save(IDbConnection db);
    }
    internal class DMStats : BaseStats
    {
        public DMStats(Player player)
            : base(player)
        {
        }
        public DMStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.DeathMatchInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                _won = record.Won;
                _loss = record.Loss;
                _kills = record.Kills;
                _killAssists = record.KillAssists;
                _deaths = record.Deaths;
                _heal = record.Heal;
            }
        }
        public float KDRate => Deaths > 0 ? (Kills * 2 + KillAssists) / (Deaths * 2) : 1.0f;
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerDeathMatchDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                Kills = Kills,
                KillAssists = KillAssists,
                Deaths = Deaths,
                Heal = Heal
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                _existsInDatabase = true;
                DbUtil.Insert(db, row);
            }
        }
        public DMStatsDto GetStatsDto()
        {
            return new DMStatsDto
            {
                Kills = ToUInt32(Kills),
                Lost = ToUInt32(Won),
                Won = ToUInt32(Loss),
                KillAssists = ToUInt32(KillAssists),
                Unk5 = 0,
                Deaths = ToUInt32(Deaths),
                Unk7 = ToUInt32(Heal),
                Unk8 = 0,
                Unk9 = 0,
                Unk10 = 0
            };
        }
        public DMUserDataDto GetUserDataDto()
        {
            var weightedKills = Kills + (KillAssists / 2.0f);
            var ratio = Deaths > 0 ? weightedKills / Deaths : Kills > 0 ? 1.0f : 0.0f;
            var roundedRatio = (float)Math.Round(ratio, 0, MidpointRounding.AwayFromZero);
            var matchCount = (float)(Won + Loss);
            return new DMUserDataDto
            {
                WinRate = WinPercent,
                KillDeathRate = roundedRatio,
                KillDeath = roundedRatio > 0.0f ? Round1((roundedRatio / (roundedRatio + 1.0f)) * 100.0f) : 0.0f,
                KillScore = Won > 0 ? Round1(Kills / (float)Won) : 0.0f,
                KillAssistScore = 0.0f,
                RecoveryScore = matchCount > 0 ? Round1(Deaths / matchCount) : 0.0f
            };
        }
        public DMUserDataScoreDto GetUserDataScoreDto()
        {
            return new DMUserDataScoreDto
            {
                TotalScore = AveragePerMatch(Kills + KillAssists + Heal),
            };
        }
    }
    internal class TDStats : BaseStats
    {
        private ulong _defense;
        private ulong _defenseAssist;
        private ulong _offense;
        private ulong _offenseAssist;
        private ulong _offenseRebound;
        private ulong _td;
        private ulong _tdassist;
        public TDStats(Player player)
            : base(player)
        {
        }
        public TDStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.TouchDownInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                _won = record.Won;
                _loss = record.Loss;
                _td = record.TD;
                _tdassist = record.TDAssist;
                _offense = record.Offense;
                _offenseAssist = record.OffenseAssist;
                _offenseRebound = record.OffenseRebound;
                _defense = record.Defense;
                _defenseAssist = record.DefenseAssist;
                _kills = record.Kill;
                _killAssists = record.KillAssist;
                _heal = record.Heal;
            }
        }
        public ulong TD
        {
            get => _td;
            set
            {
                if (_td == value)
                    return;
                _td = value;
                _needsSave = true;
            }
        }
        public ulong TDAssist
        {
            get => _tdassist;
            set
            {
                if (_tdassist == value)
                    return;
                _tdassist = value;
                _needsSave = true;
            }
        }
        public ulong Offense
        {
            get => _offense;
            set
            {
                if (_offense == value)
                    return;
                _offense = value;
                _needsSave = true;
            }
        }
        public ulong OffenseAssist
        {
            get => _offenseAssist;
            set
            {
                if (_offenseAssist == value)
                    return;
                _offenseAssist = value;
                _needsSave = true;
            }
        }
        public ulong OffenseRebound
        {
            get => _offenseRebound;
            set
            {
                if (_offenseRebound == value)
                    return;
                _offenseRebound = value;
                _needsSave = true;
            }
        }
        public ulong Defense
        {
            get => _defense;
            set
            {
                if (_defense == value)
                    return;
                _defense = value;
                _needsSave = true;
            }
        }
        public ulong DefenseAssist
        {
            get => _defenseAssist;
            set
            {
                if (_defenseAssist == value)
                    return;
                _defenseAssist = value;
                _needsSave = true;
            }
        }
        public ulong TotalScore => 10 * TD + 5 * TDAssist + 4 * Offense +
            2 * OffenseAssist + 4 * Defense + 2 * DefenseAssist +
            2 * Kills + KillAssists + 2 * Heal;
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerTouchDownDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                TD = TD,
                TDAssist = TDAssist,
                Offense = Offense,
                OffenseAssist = OffenseAssist,
                Defense = Defense,
                DefenseAssist = DefenseAssist,
                Kill = Kills,
                KillAssist = KillAssists,
                OffenseRebound = OffenseRebound,
                Heal = Heal
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                DbUtil.Insert(db, row);
                _existsInDatabase = true;
            }
        }
        public TDStatsDto GetStatsDto()
        {
            return new TDStatsDto
            {
                Unk1 = ToUInt32(Won),
                Unk2 = ToUInt32(Loss),
                Unk3 = ToUInt32(TD),
                Unk4 = 0,
                Unk5 = ToUInt32(TDAssist),
                Unk6 = ToUInt32(Kills),
                Unk7 = ToUInt32(KillAssists),
                Unk8 = ToUInt32(Defense),
                Unk9 = ToUInt32(DefenseAssist),
                Unk10 = ToUInt32(Offense),
                Unk11 = ToUInt32(OffenseAssist),
                Unk12 = ToUInt32(Heal),
                Unk13 = ToUInt32(OffenseRebound),
                Unk14 = 0,
                Unk15 = 0,
                Unk16 = 0,
                Unk17 = 0,
                Unk18 = 0
            };
        }
        public TDUserDataDto GetUserDataDto()
        {
            return new TDUserDataDto
            {
                WinRate = Round1(WinRate * 100.0f),
                TDScore = AveragePerMatch(10 * TD + 5 * TDAssist),
                TDSucc = TD + (TDAssist / 2.0f),
                DefenseScore = AveragePerMatch(4 * Defense + 2 * DefenseAssist),
                OffenseScore = AveragePerMatch(4 * Offense + 2 * OffenseAssist),
                KillScore = AveragePerMatch(2 * Kills + KillAssists),
                RecoveryScore = AveragePerMatch(2 * Heal)
            };
        }
        public TDUserDataScoreDto GetUserDataScoreDto()
        {
            return new TDUserDataScoreDto
            {
                TotalScore = AveragePerMatch(TotalScore)
            };
        }
    }
    internal class ChaserStats : BaseStats
    {
        private ulong _chasedRound;
        private ulong _chasedWon;
        private ulong _chaserRounds;
        private ulong _chaserWon;
        private ulong _kills;
        public ChaserStats(Player player)
            : base(player)
        {
        }
        public ChaserStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.ChaserInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                _chasedWon = record.ChasedWon;
                _chasedRound = record.ChasedRounds;
                _chaserWon = record.ChaserWon;
                _chaserRounds = record.ChaserRounds;
                _kills = record.Kills;
            }
        }
        public ulong ChasedWon
        {
            get => _chasedWon;
            set
            {
                if (_chasedWon == value)
                    return;
                _chasedWon = value;
                _needsSave = true;
            }
        }
        public ulong ChasedRounds
        {
            get => _chasedRound;
            set
            {
                if (_chasedRound == value)
                    return;
                _chasedRound = value;
                _needsSave = true;
            }
        }
        public ulong ChaserWon
        {
            get => _chaserWon;
            set
            {
                if (_chaserWon == value)
                    return;
                _chaserWon = value;
                _needsSave = true;
            }
        }
        public ulong ChaserRounds
        {
            get => _chaserRounds;
            set
            {
                if (_chaserRounds == value)
                    return;
                _chaserRounds = value;
                _needsSave = true;
            }
        }
        public ulong Killed
        {
            get => _kills;
            set
            {
                if (_kills == value)
                    return;
                _kills = value;
                _needsSave = true;
            }
        }
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerChaserDto
            {
                PlayerId = (int)Player.Account.Id,
                ChasedRounds = ChasedRounds,
                ChasedWon = ChasedWon,
                ChaserRounds = ChaserRounds,
                ChaserWon = ChaserWon,
                Kills = Killed
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                _existsInDatabase = true;
                DbUtil.Insert(db, row);
            }
        }
        public ChaserStatsDto GetStatsDto()
        {
            return new ChaserStatsDto
            {
                ChasedWon = ToUInt32(ChasedWon),
                ChasedRounds = ToUInt32(ChasedRounds),
                ChaserWon = ToUInt32(ChaserWon),
                ChaserRounds = ToUInt32(ChaserRounds),
                Kills = ToUInt32(Player.stats.DeathMatch.Kills)
            };
        }
        public ChaserUserDataDto GetUserDataDto()
        {
            var catchRatio = ChaserRounds > 0 ? ChaserWon / (float)ChaserRounds : 0.0f;
            var killsPerRound = ChasedRounds > 0 ? Player.stats.DeathMatch.Kills / (float)ChasedRounds : 0.0f;
            return new ChaserUserDataDto
            {
                KillProbability = Round1(catchRatio * 100.0f),
                Kills = Round1(killsPerRound)
            };
        }
        public ChaserUserDataScoreDto GetUserDataScoreDto()
        {
            var escapeRatio = ChasedRounds > 0 ? ChasedWon / (float)ChasedRounds : 1.0f;
            return new ChaserUserDataScoreDto
            {
                SurvivalProbability = Round1(escapeRatio * 100.0f)
            };
        }
    }
    internal class BRStats : BaseStats
    {
        private ulong _firstKillAssists;
        private ulong _firstKilled;
        private ulong _firstPlace;
        public BRStats(Player player)
            : base(player)
        {
        }
        public BRStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.BattleRoyalInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                Won = record.Won;
                Loss = record.Loss;
                Kills = record.Kills;
                KillAssists = record.KillAssists;
                FirstKilled = record.FirstKilled;
                FirstPlace = record.FirstPlace;
            }
        }
        public ulong FirstKilled
        {
            get => _firstKilled;
            set
            {
                if (_firstKilled == value)
                    return;
                _needsSave = true;
                _firstKilled = value;
            }
        }
        public ulong FirstKillAssists
        {
            get => _firstKillAssists;
            set
            {
                if (_firstKillAssists == value)
                    return;
                _needsSave = true;
                _firstKillAssists = value;
            }
        }
        public ulong FirstPlace
        {
            get => _firstPlace;
            set
            {
                if (_firstPlace == value)
                    return;
                _needsSave = true;
                _firstPlace = value;
            }
        }
        public float BRScore => WinPercent;
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerBattleRoyalDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                KillAssists = KillAssists,
                Kills = Kills,
                FirstKilled = FirstKilled,
                FirstPlace = FirstPlace
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                DbUtil.Insert(db, row);
                _existsInDatabase = true;
            }
        }
        public BRStatsDto GetStatsDto()
        {
            var scoreTenths = (uint)Math.Round(BRScore * 10.0f);
            return new BRStatsDto
            {
                Won = scoreTenths,
                Lost = 10,
                TotalScore = BRScore,
                FirstKilled = ToUInt32(FirstKilled),
                FirstPlace = ToUInt32(FirstPlace)
            };
        }
        public BRUserDataDto GetUserDataDto()
        {
            return new BRUserDataDto
            {
                CountFirstPlaceKilled = ToUInt32(FirstKilled),
                CountFirstPlace = ToUInt32(FirstPlace)
            };
        }
        public BRUserDataScoreDto GetUserDataScoreDto()
        {
            return new BRUserDataScoreDto
            {
                TotalScore = BRScore
            };
        }
    }
    internal class CPTStats : BaseStats
    {
        private ulong _cptCount;
        private ulong _cptKills;
        public CPTStats(Player player)
            : base(player)
        {
        }
        public CPTStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.CaptainInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                _won = record.Won;
                _loss = record.Loss;
                _cptKills = record.CPTKilled;
                _cptCount = record.CPTCount;
            }
        }
        public ulong CPTKilled
        {
            get => _cptKills;
            set
            {
                if (_cptKills == value)
                    return;
                _needsSave = true;
                _cptKills = value;
            }
        }
        public ulong CPTCount
        {
            get => _cptCount;
            set
            {
                if (_cptCount == value)
                    return;
                _needsSave = true;
                _cptCount = value;
            }
        }
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerCaptainDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                CPTCount = CPTCount,
                CPTKilled = CPTKilled
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                DbUtil.Insert(db, row);
                _existsInDatabase = true;
            }
        }
        public CPTStatsDto GetStatsDto()
        {
            var captainScore = Round1(Loss > 0 ? Won / (float)Loss : Won);
            var captainScoreTenths = (uint)Math.Round(captainScore * 10.0f);
            return new CPTStatsDto
            {
                Won = captainScoreTenths,
                Lost = 10,
                TotalScore = captainScore,
                CaptainKilled = ToUInt32(CPTKilled),
                Captain = ToUInt32(CPTCount),
            };
        }
        public CPTUserDataDto GetUserDataDto()
        {
            return new CPTUserDataDto
            {
                Kills = ToUInt32(CPTKilled),
                Domination = ToUInt32(CPTCount)
            };
        }
        public CPTUserDataScoreDto GetUserDataScoreDto()
        {
            return new CPTUserDataScoreDto
            {
                TotalScore = Round1(Loss > 0 ? Won / (float)Loss : Won),
            };
        }
    }
    internal class SiegeStats : BaseStats
    {
        private ulong _battleScore;
        private ulong _captureScore;
        private ulong _itemObtainScore;
        private ulong _mainCoreCaptureScore;
        public SiegeStats(Player plater)
            : base(plater)
        {
        }
        public SiegeStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.SiegeInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                Won = record.Won;
                Loss = record.Loss;
                _captureScore = record.CaptureScore;
                _battleScore = record.BattleScore;
                _mainCoreCaptureScore = record.MainCoreCaptureScore;
                _itemObtainScore = record.ItemObtainScore;
            }
        }
        public ulong BattleScore
        {
            get => _battleScore;
            set
            {
                if (_battleScore == value)
                    return;
                _battleScore = value;
                _needsSave = true;
            }
        }
        public ulong CaptureScore
        {
            get => _captureScore;
            set
            {
                if (_captureScore == value)
                    return;
                _captureScore = value;
                _needsSave = true;
            }
        }
        public ulong MainCoreCaptureScore
        {
            get => _mainCoreCaptureScore;
            set
            {
                if (_mainCoreCaptureScore == value)
                    return;
                _mainCoreCaptureScore = value;
                _needsSave = true;
            }
        }
        public ulong ItemObtainScore
        {
            get => _itemObtainScore;
            set
            {
                if (_itemObtainScore == value)
                    return;
                _itemObtainScore = value;
                _needsSave = true;
            }
        }
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerSiegeDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                CaptureScore = _captureScore,
                BattleScore = _battleScore,
                MainCoreCaptureScore = _mainCoreCaptureScore,
                ItemObtainScore = _itemObtainScore
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                DbUtil.Insert(db, row);
                _existsInDatabase = true;
            }
        }
        public SiegeStatsDto GetStatsDto()
        {
            var matchCount = Won + Loss;
            return new SiegeStatsDto
            {
                Unk1 = 0,
                Unk2 = 0,
                Unk3 = ToUInt32(BattleScore),
                Unk4 = 0,
                Unk5 = ToUInt32(MainCoreCaptureScore),
                Unk6 = 0,
                Unk7 = ToUInt32(ItemObtainScore),
                Unk8 = 0,
                Unk9 = ToUInt32(matchCount > 0 ? Won : 1),
                Unk10 = ToUInt32(Loss),
                Unk11 = 0
            };
        }
        public SiegeUserDataDto GetUserDataDto()
        {
            return new SiegeUserDataDto
            {
                WinRate = WinPercent,
                CaptureScore = AveragePerMatch(CaptureScore),
                BattleScore = AveragePerMatch(BattleScore),
                MainCoreCaptureScore = AveragePerMatch(MainCoreCaptureScore),
                ItemObtainScore = AveragePerMatch(ItemObtainScore)
            };
        }
        public float GetUserDataScore()
        {
            return AveragePerMatch(CaptureScore + BattleScore + MainCoreCaptureScore + ItemObtainScore);
        }
    }
    internal class ArenaStats : BaseStats
    {
        private ulong _doubleKills;
        private ulong _leaderKills;
        private ulong _leaderSelected;
        private ulong _shortestKillTime;
        private ulong _totalScore;
        private ulong _tripleKills;
        public ArenaStats(Player plater)
            : base(plater)
        { }
        public ArenaStats(Player player, PlayerDto playerDto)
            : base(player)
        {
            var record = playerDto.ArenaInfo.FirstOrDefault();
            _existsInDatabase = false;
            if (record != null)
            {
                _existsInDatabase = true;
                Won = record.Won;
                Loss = record.Loss;
                Kills = record.Kills;
                Deaths = record.Deaths;
                _doubleKills = record.DoubleKills;
                _tripleKills = record.TripleKills;
                _shortestKillTime = record.ShortestKillTime;
                _leaderSelected = record.LeaderSelected;
                _leaderKills = record.LeaderKills;
                _totalScore = record.TotalScore;
            }
        }
        public ulong DoubleKills
        {
            get => _doubleKills;
            set
            {
                if (_doubleKills == value)
                    return;
                _doubleKills = value;
                _needsSave = true;
            }
        }
        public ulong TripleKills
        {
            get => _tripleKills;
            set
            {
                if (_tripleKills == value)
                    return;
                _tripleKills = value;
                _needsSave = true;
            }
        }
        public ulong ShortestKillTime
        {
            get => _shortestKillTime;
            set
            {
                if (_shortestKillTime == value)
                    return;
                _shortestKillTime = value;
                _needsSave = true;
            }
        }
        public ulong LeaderSelected
        {
            get => _leaderSelected;
            set
            {
                if (_leaderSelected == value)
                    return;
                _leaderSelected = value;
                _needsSave = true;
            }
        }
        public ulong LeaderKills
        {
            get => _leaderKills;
            set
            {
                if (_leaderKills == value)
                    return;
                _leaderKills = value;
                _needsSave = true;
            }
        }
        public ulong TotalScore
        {
            get => _totalScore;
            set
            {
                if (_totalScore == value)
                    return;
                _totalScore = value;
                _needsSave = true;
            }
        }
        private float DoubleKillDisplayValue => RoundPercent1(DoubleKills);
        private float TripleKillDisplayValue => RoundPercent1(TripleKills);
        private float RoundPercent1(ulong value)
        {
            if (TotalMatches <= 0)
                return 0.0f;
            return (float)Math.Floor((value * 1000.0f / TotalMatches) + 0.5f) / 10.0f;
        }
        public override void Save(IDbConnection db)
        {
            if (!_needsSave)
                return;
            var row = new PlayerArenaDto
            {
                PlayerId = (int)Player.Account.Id,
                Won = Won,
                Loss = Loss,
                Kills = Kills,
                Deaths = Deaths,
                DoubleKills = DoubleKills,
                TripleKills = TripleKills,
                ShortestKillTime = ShortestKillTime,
                LeaderSelected = LeaderSelected,
                LeaderKills = LeaderKills,
                TotalScore = TotalScore
            };
            if (_existsInDatabase)
            {
                DbUtil.Update(db, row);
            }
            else
            {
                DbUtil.Insert(db, row);
                _existsInDatabase = true;
            }
        }
        public ArenaStatsDto GetStatsDto()
        {
            return new ArenaStatsDto
            {
                Unk1 = ToUInt32(ShortestKillTime),
                Unk2 = ToUInt32(Won),
                Unk3 = ToUInt32(Loss),
                Unk4 = ToUInt32(Kills),
                Unk5 = ToUInt32(Deaths),
                Unk6 = ToUInt32(DoubleKills),
                Unk7 = ToUInt32(TripleKills),
                Unk8 = ToUInt32(LeaderSelected),
                Unk9 = ToUInt32(LeaderKills),
                Unk10 = ToUInt32(TotalScore),
                Unk11 = 0,
                Unk12 = 0,
                Unk13 = 0,
                Unk14 = 0
            };
        }
        public ArenaUserDataDto GetUserDataDto()
        {
            var killDeathTotal = Kills + Deaths;
            var kdRate = Deaths > 0 ? Kills / (float)Deaths : Kills > 0 ? 1.0f : 0.0f;
            return new ArenaUserDataDto
            {
                WinRate = Round1(WinRate * 100.0f),
                KdRate = Round1(kdRate),
                KdPercent = killDeathTotal > 0 ? Round1((Kills / (float)killDeathTotal) * 100.0f) : 0.0f,
                DoubleKillRate = DoubleKillDisplayValue,
                TripleKillRate = TripleKillDisplayValue,
                ShortestKillTime = ToUInt32(ShortestKillTime),
                LeaderSelected = ToUInt32(LeaderSelected),
                LeaderKills = ToUInt32(LeaderKills)
            };
        }
        public float GetUserDataScore()
        {
            return AveragePerMatch(TotalScore, 0.0f);
        }
    }
}

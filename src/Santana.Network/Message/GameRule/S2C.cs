using System;
using SantanaLib.Serialization;
using Santana.Network.Data.GameRule;
using Santana.Network.Message.GameRule;
using Santana.Network.Serializers;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.GameRule
{
    [Packet(3001, PacketType.GameRule)]
    public class RoomEnterPlayerAckMessage
    {
        public RoomEnterPlayerAckMessage()
        {
            Nickname = "";
        }
        public RoomEnterPlayerAckMessage(ulong accountId, string nickname, byte unk1, PlayerGameMode mode, int unk3)
        {
            AccountId = accountId;
            Unk1 = unk1;
            PlayerGameMode = mode;
            ClanId = unk3;
            Nickname = nickname;
        }
        public ulong AccountId { get; set; }
        public byte Unk1 { get; set; }
        public PlayerGameMode PlayerGameMode { get; set; }
        public int ClanId { get; set; }
        public string Nickname { get; set; }
        public Team Team { get; set; }
    }
    [Packet(3002, PacketType.GameRule)]
    public class RoomLeavePlayerAckMessage
    {
        public RoomLeavePlayerAckMessage()
        {
            Nickname = "";
        }
        public RoomLeavePlayerAckMessage(ulong accountId, string nickname, RoomLeaveReason reason)
        {
            AccountId = accountId;
            Nickname = nickname;
            Reason = reason;
        }
        public ulong AccountId { get; set; }
        public string Nickname { get; set; }
        public RoomLeaveReason Reason { get; set; }
    }
    [Packet(3003, PacketType.GameRule)]
    public class RoomLeaveReqeustAckMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(3004, PacketType.GameRule)]
    public class RoomChangeTeamAckMessage
    {
        public RoomChangeTeamAckMessage()
        {
        }
        public RoomChangeTeamAckMessage(ulong accountId, Team team, PlayerGameMode mode)
        {
            AccountId = accountId;
            Team = team;
            Mode = mode;
        }
        public ulong AccountId { get; set; }
        public Team Team { get; set; }
        public PlayerGameMode Mode { get; set; }
    }
    [Packet(3005, PacketType.GameRule)]
    public class RoomChangeTeamFailAckMessage
    {
        public RoomChangeTeamFailAckMessage()
        {
        }
        public RoomChangeTeamFailAckMessage(ChangeTeamResult result)
        {
            Result = result;
        }
        public ChangeTeamResult Result { get; set; }
    }
    [Packet(3006, PacketType.GameRule)]
    public class RoomChoiceTeamChangeAckMessage
    {
        public RoomChoiceTeamChangeAckMessage()
        {
        }
        public RoomChoiceTeamChangeAckMessage(ulong playerToMove, ulong playerToReplace, Team fromTeam, Team toTeam)
        {
            PlayerToMove = playerToMove;
            PlayerToReplace = playerToReplace;
            FromTeam = fromTeam;
            ToTeam = toTeam;
        }
        public ulong PlayerToMove { get; set; }
        public ulong PlayerToReplace { get; set; }
        public Team FromTeam { get; set; }
        public Team ToTeam { get; set; }
    }
    [Packet(3007, PacketType.GameRule)]
    public class RoomChoiceTeamChangeFailAckMessage
    {
        public byte Result { get; set; }
    }
    [Packet(3008, PacketType.GameRule)]
    public class GameEventMessageAckMessage
    {
        public GameEventMessageAckMessage()
        {
            String = "";
        }
        public GameEventMessageAckMessage(GameEventMessage @event, ulong accountId, uint unk, ushort value,
            string @string)
        {
            Event = @event;
            AccountId = accountId;
            Unk = unk;
            Value = value;
            String = @string;
        }
        public GameEventMessage Event { get; set; }
        public ulong AccountId { get; set; }
        public uint Unk { get; set; }
        public ushort Value { get; set; }
        public string String { get; set; }
    }
    [Packet(3009, PacketType.GameRule)]
    public class GameBriefingInfoAckMessage
    {
        public GameBriefingInfoAckMessage()
        {
            Data = Array.Empty<byte>();
        }
        public GameBriefingInfoAckMessage(bool isResult, bool isEvent, byte[] data)
        {
            IsResult = isResult;
            IsEvent = isEvent;
            Data = data;
        }
        public bool IsResult { get; set; }
        public bool IsEvent { get; set; }
        [Scalar] public byte[] Data { get; set; }
    }
    [Packet(3010, PacketType.GameRule)]
    public class GameChangeStateAckMessage
    {
        public GameChangeStateAckMessage()
        {
        }
        public GameChangeStateAckMessage(GameState state)
        {
            State = state;
        }
        public GameState State { get; set; }
    }
    [Packet(3011, PacketType.GameRule)]
    public class GameChangeSubStateAckMessage
    {
        public GameChangeSubStateAckMessage()
        {
        }
        public GameChangeSubStateAckMessage(GameTimeState state)
        {
            State = state;
        }
        public GameTimeState State { get; set; }
    }
    [Packet(3012, PacketType.GameRule)]
    public class GameDestroyGameRuleAckMessage
    {
    }
    [Packet(3013, PacketType.GameRule)]
    public class RoomChangeMasterAckMessage
    {
        public RoomChangeMasterAckMessage()
        {
        }
        public RoomChangeMasterAckMessage(ulong accountId)
        {
            AccountId = accountId;
        }
        public ulong AccountId { get; set; }
    }
    [Packet(3014, PacketType.GameRule)]
    public class RoomChangeRefereeAckMessage
    {
        public RoomChangeRefereeAckMessage()
        {
        }
        public RoomChangeRefereeAckMessage(ulong accountId)
        {
            AccountId = accountId;
        }
        public ulong AccountId { get; set; }
    }
    [Packet(3015, PacketType.GameRule)]
    public class SlaughterChangeSlaughterAckMessage
    {
        public SlaughterChangeSlaughterAckMessage()
        {
            Unk = Array.Empty<ulong>();
        }
        public SlaughterChangeSlaughterAckMessage(ulong accountId)
        {
            AccountId = accountId;
            Unk = Array.Empty<ulong>();
        }
        public SlaughterChangeSlaughterAckMessage(ulong accountId, ulong[] unk)
        {
            AccountId = accountId;
            Unk = unk;
        }
        public ulong AccountId { get; set; }
        public ulong[] Unk { get; set; }
    }
    [Packet(3016, PacketType.GameRule)]
    public class RoomReadyRoundAckMessage
    {
        public RoomReadyRoundAckMessage()
        {
        }
        public RoomReadyRoundAckMessage(ulong accountId, bool isReady)
        {
            AccountId = accountId;
            IsReady = isReady;
        }
        public ulong AccountId { get; set; }
        public bool IsReady { get; set; }
        public byte Result { get; set; }
    }
    [Packet(3017, PacketType.GameRule)]
    public class RoomBeginRoundAckMessage
    {
    }
    [Packet(3018, PacketType.GameRule)]
    public class GameAvatarChangeAckMessage
    {
        public GameAvatarChangeAckMessage()
        {
            Unk1 = new ChangeAvatarUnk1Dto();
            Unk2 = Array.Empty<ChangeAvatarUnk2Dto>();
        }
        public GameAvatarChangeAckMessage(ChangeAvatarUnk1Dto unk1, ChangeAvatarUnk2Dto[] unk2)
        {
            Unk1 = unk1;
            Unk2 = unk2;
        }
        public ChangeAvatarUnk1Dto Unk1 { get; set; }
        public ChangeAvatarUnk2Dto[] Unk2 { get; set; }
    }
    [Packet(3019, PacketType.GameRule)]
    public class RoomChangeRuleNotifyAckMessage
    {
        public RoomChangeRuleNotifyAckMessage()
        {
            Settings = new ChangeRuleDto();
        }
        public RoomChangeRuleNotifyAckMessage(ChangeRuleDto settings)
        {
            Settings = settings;
        }
        public ChangeRuleDto Settings { get; set; }
    }
    [Packet(3093, PacketType.GameRule)]
    public class RoomChangeRuleNotifyAck2Message
    {
        public RoomChangeRuleNotifyAck2Message()
        {
            Settings = new ChangeRuleDto2();
        }
        public RoomChangeRuleNotifyAck2Message(ChangeRuleDto2 settings)
        {
            Settings = settings;
        }
        public ChangeRuleDto2 Settings { get; set; }
    }
    [Packet(3020, PacketType.GameRule)]
    public class RoomChangeRuleAckMessage
    {
        public RoomChangeRuleAckMessage()
        {
            Settings = new ChangeRuleDto2();
        }
        public RoomChangeRuleAckMessage(ChangeRuleDto2 settings)
        {
            Settings = settings;
        }
        public ChangeRuleDto2 Settings { get; set; }
    }
    [Packet(3021, PacketType.GameRule)]
    public class RoomChangeRuleFailAckMessage
    {
        public byte Result { get; set; }
    }
    [Packet(3022, PacketType.GameRule)]
    public class ScoreMissionScoreAckMessage
    {
        public ulong AccountId { get; set; }
        public int Score { get; set; }
    }
    [Packet(3095, PacketType.GameRule)]
    public class ScoreAIKillAckMessage
    {
        public ScoreAIKillAckMessage()
        {
        }
        public ScoreAIKillAckMessage(LongPeerId unk)
        {
            Unk = unk;
        }
        public LongPeerId Unk { get; set; }
    }
    [Packet(3100, PacketType.GameRule)]
    public class ArenaSetGameOptionAckMessage
    {
        public ArenaSetGameOptionAckMessage()
        {
        }
        public ArenaSetGameOptionAckMessage(int unk)
        {
            Unk = unk;
        }
        public int Unk { get; set; }
    }
    [Packet(2067, PacketType.GameRule)]
    public class ArenaSpecialPointReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2068, PacketType.GameRule)]
    public class ArenaDrawHealthPointAckMessage
    {
        public byte Unk1 { get; set; }
        public ulong Unk2 { get; set; }
        public ulong Unk3 { get; set; }
        public int Unk4 { get; set; }
    }
    [Packet(3023, PacketType.GameRule)]
    public class ScoreKillAckMessage
    {
        public ScoreKillAckMessage()
        {
            Score = new ScoreDto();
        }
        public ScoreKillAckMessage(ScoreDto score)
        {
            Score = score;
        }
        public ScoreDto Score { get; set; }
    }
    [Packet(3024, PacketType.GameRule)]
    public class ScoreKillAssistAckMessage
    {
        public ScoreKillAssistAckMessage()
        {
            Score = new ScoreAssistDto();
        }
        public ScoreKillAssistAckMessage(ScoreAssistDto score)
        {
            Score = score;
        }
        public ScoreAssistDto Score { get; set; }
    }
    [Packet(3025, PacketType.GameRule)]
    public class ScoreOffenseAckMessage
    {
        public ScoreOffenseAckMessage()
        {
            Score = new ScoreDto();
        }
        public ScoreOffenseAckMessage(ScoreDto score)
        {
            Score = score;
        }
        public ScoreDto Score { get; set; }
    }
    [Packet(3026, PacketType.GameRule)]
    public class ScoreOffenseAssistAckMessage
    {
        public ScoreOffenseAssistAckMessage()
        {
            Score = new ScoreAssistDto();
        }
        public ScoreOffenseAssistAckMessage(ScoreAssistDto score)
        {
            Score = score;
        }
        public ScoreAssistDto Score { get; set; }
    }
    [Packet(3027, PacketType.GameRule)]
    public class ScoreDefenseAckMessage
    {
        public ScoreDefenseAckMessage()
        {
            Score = new ScoreDto();
        }
        public ScoreDefenseAckMessage(ScoreDto score)
        {
            Score = score;
        }
        public ScoreDto Score { get; set; }
    }
    [Packet(3028, PacketType.GameRule)]
    public class ScoreDefenseAssistAckMessage
    {
        public ScoreDefenseAssistAckMessage()
        {
            Score = new ScoreAssistDto();
        }
        public ScoreDefenseAssistAckMessage(ScoreAssistDto score)
        {
            Score = score;
        }
        public ScoreAssistDto Score { get; set; }
    }
    [Packet(3030, PacketType.GameRule)]
    public class ScoreHealAssistAckMessage
    {
        public ScoreHealAssistAckMessage()
        {
            Id = 0;
        }
        public ScoreHealAssistAckMessage(LongPeerId id)
        {
            Id = id;
        }
        public LongPeerId Id { get; set; }
    }
    [Packet(3031, PacketType.GameRule)]
    public class ScoreGoalAckMessage
    {
        public ScoreGoalAckMessage()
        {
            Id = 0;
        }
        public ScoreGoalAckMessage(LongPeerId id)
        {
            Id = id;
        }
        public LongPeerId Id { get; set; }
    }
    [Packet(3032, PacketType.GameRule)]
    public class ScoreGoalAssistAckMessage
    {
        public ScoreGoalAssistAckMessage()
        {
            Id = 0;
            Assist = 0;
        }
        public ScoreGoalAssistAckMessage(LongPeerId id, LongPeerId assist)
        {
            Id = id;
            Assist = assist;
        }
        public LongPeerId Id { get; set; }
        public LongPeerId Assist { get; set; }
    }
    [Packet(3033, PacketType.GameRule)]
    public class ScoreReboundAckMessage
    {
        public ScoreReboundAckMessage()
        {
            NewId = 0;
            OldId = 0;
        }
        public ScoreReboundAckMessage(LongPeerId newId, LongPeerId oldId)
        {
            NewId = newId;
            OldId = oldId;
        }
        public LongPeerId NewId { get; set; }
        public LongPeerId OldId { get; set; }
    }
    [Packet(3034, PacketType.GameRule)]
    public class ScoreSuicideAckMessage
    {
        public ScoreSuicideAckMessage()
        {
            Id = 0;
        }
        public ScoreSuicideAckMessage(LongPeerId id, AttackAttribute icon)
        {
            Id = id;
            Icon = icon;
        }
        public LongPeerId Id { get; set; }
        [Wire(Kind.UInt)] public AttackAttribute Icon { get; set; }
    }
    [Packet(3035, PacketType.GameRule)]
    public class ScoreTeamKillAckMessage
    {
        public ScoreTeamKillAckMessage()
        {
            Score = new Score2Dto();
        }
        public ScoreTeamKillAckMessage(Score2Dto score)
        {
            Score = score;
        }
        public Score2Dto Score { get; set; }
    }
    [Packet(3036, PacketType.GameRule)]
    public class SlaughterRoundWinAckMessage
    {
        public SlaughterRoundWinAckMessage()
        {
        }
        public SlaughterRoundWinAckMessage(byte unk)
        {
            Unk = unk;
        }
        public byte Unk { get; set; }
    }
    [Packet(3037, PacketType.GameRule)]
    public class SlaughterSLRoundWinAckMessage
    {
    }
    [Packet(3038, PacketType.GameRule)]
    public class RoomChangeItemAckMessage
    {
        public RoomChangeItemAckMessage()
        {
            Unk1 = new ChangeItemsUnkDto();
            Unk2 = Array.Empty<ChangeAvatarUnk2Dto>();
        }
        public RoomChangeItemAckMessage(ChangeItemsUnkDto unk1, ChangeAvatarUnk2Dto[] unk2)
        {
            Unk1 = unk1;
            Unk2 = unk2;
        }
        public ChangeItemsUnkDto Unk1 { get; set; }
        public ChangeAvatarUnk2Dto[] Unk2 { get; set; }
    }
    [Packet(3039, PacketType.GameRule)]
    public class RoomPlayModeChangeAckMessage
    {
        public RoomPlayModeChangeAckMessage()
        {
        }
        public RoomPlayModeChangeAckMessage(ulong accountId, PlayerGameMode mode)
        {
            AccountId = accountId;
            Mode = mode;
        }
        public ulong AccountId { get; set; }
        public PlayerGameMode Mode { get; set; }
    }
    [Packet(3040, PacketType.GameRule)]
    public class GameRefreshGameRuleInfoAckMessage
    {
        public GameRefreshGameRuleInfoAckMessage()
        {
        }
        public GameRefreshGameRuleInfoAckMessage(GameState _GameState, GameTimeState _GameTimeState, TimeSpan _ElapsedTime)
        {
            GameState = _GameState;
            GameTimeState = _GameTimeState;
            ElapsedTime = _ElapsedTime;
        }
        public GameState GameState { get; set; }
        public GameTimeState GameTimeState { get; set; }
        public TimeSpan ElapsedTime { get; set; }
    }
    [Packet(3041, PacketType.GameRule)]
    public class ArcadeScoreSyncAckMessage
    {
        public ArcadeScoreSyncAckMessage()
        {
        }
        public ArcadeScoreSyncAckMessage(ArcadeScoreSyncReqDto[] scores)
        {
            Scores = scores;
        }
        public ArcadeScoreSyncReqDto[] Scores { get; set; }
    }
    [Packet(3042, PacketType.GameRule)]
    public class ArcadeBeginRoundAckMessage
    {
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
    }
    [Packet(3043, PacketType.GameRule)]
    public class ArcadeStageBriefingAckMessage
    {
        public ArcadeStageBriefingAckMessage()
        {
            Data = Array.Empty<byte>();
        }
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        [Scalar] public byte[] Data { get; set; }
    }
    [Packet(3044, PacketType.GameRule)]
    public class ArcadeEnablePlayTimeAckMessage
    {
        public ArcadeEnablePlayTimeAckMessage()
        {
        }
        public ArcadeEnablePlayTimeAckMessage(byte unk)
        {
            Unk = unk;
        }
        public byte Unk { get; set; }
    }
    [Packet(3045, PacketType.GameRule)]
    public class ArcadeStageInfoAckMessage
    {
        public ArcadeStageInfoAckMessage()
        {
        }
        public ArcadeStageInfoAckMessage(byte unk, int unk2)
        {
            Unk1 = unk;
            Unk2 = unk2;
        }
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(3046, PacketType.GameRule)]
    public class ArcadeRespawnAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(3047, PacketType.GameRule)]
    public class ArcadeDeathPlayerInfoAckMessage
    {
        public ArcadeDeathPlayerInfoAckMessage()
        {
            Players = Array.Empty<ulong>();
        }
        public byte Unk { get; set; }
        public ulong[] Players { get; set; }
    }
    [Packet(3048, PacketType.GameRule)]
    public class ArcadeStageReadyAckMessage
    {
        public ulong AccountId { get; set; }
    }
    [Packet(3049, PacketType.GameRule)]
    public class ArcadeRespawnFailAckMessage
    {
        public uint Result { get; set; }
    }
    [Packet(3050, PacketType.GameRule)]
    public class AdminChangeHPAckMessage
    {
        public float Value { get; set; }
    }
    [Packet(3051, PacketType.GameRule)]
    public class AdminChangeMPAckMessage
    {
        public float Value { get; set; }
    }
    [Packet(3052, PacketType.GameRule)]
    public class ArcadeChangeStageAckMessage
    {
        public byte Stage { get; set; }
    }
    [Packet(3053, PacketType.GameRule)]
    public class ArcadeStageSelectAckMessage
    {
        public ArcadeStageSelectAckMessage()
        {
        }
        public ArcadeStageSelectAckMessage(byte unk1, byte unk2)
        {
            Unk1 = unk1;
            Unk2 = unk2;
        }
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
    }
    [Packet(3054, PacketType.GameRule)]
    public class ArcadeSaveDateInfoAckMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(3055, PacketType.GameRule)]
    public class SlaughterAttackPointAckMessage
    {
        public ulong AccountId { get; set; }
        public float Unk1 { get; set; }
        public float Unk2 { get; set; }
    }
    [Packet(3056, PacketType.GameRule)]
    public class SlaughterHealPointAckMessage
    {
        public ulong AccountId { get; set; }
        public float Unk { get; set; }
    }
    [Packet(3057, PacketType.GameRule)]
    public class SlaughterChangeBonusTargetAckMessage
    {
        public SlaughterChangeBonusTargetAckMessage()
        {
        }
        public SlaughterChangeBonusTargetAckMessage(ulong accountId)
        {
            AccountId = accountId;
        }
        public ulong AccountId { get; set; }
    }
    [Packet(3058, PacketType.GameRule)]
    public class ArcadeSucceedLoadingAckMessage
    {
        public ulong AccountId { get; set; }
    }
    [Packet(3059, PacketType.GameRule)]
    public class MoneyUseCoinAckMessage
    {
        public UseCoinMessage Message { get; set; }
        public BuffType BuffType { get; set; }
        public int Time { get; set; }
        public int Value { get; set; }
        public byte Unk5 { get; set; }
    }
    [Packet(3060, PacketType.GameRule)]
    public class GameLuckyShotAckMessage
    {
        public LuckyShotType LuckyShotType { get; set; }
        public int Value { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(3061, PacketType.GameRule)]
    public class FreeAllForChangeTheFirstAckMessage
    {
        public FreeAllForChangeTheFirstAckMessage()
        {
        }
        public FreeAllForChangeTheFirstAckMessage(ulong accountId)
        {
            AccountId = accountId;
        }
        public ulong AccountId { get; set; }
    }
    [Packet(3062, PacketType.GameRule)]
    public class LogDevLogStartAckMessage
    {
    }
    [Packet(3063, PacketType.GameRule)]
    public class GameKickOutRequestAckMessage
    {
        public VoteKickMessage Message { get; set; }
    }
    [Packet(3064, PacketType.GameRule)]
    public class GameKickOutVoteResultAckMessage
    {
        public VoteKickResult Result { get; set; }
    }
    [Packet(3065, PacketType.GameRule)]
    public class GameKickOutStateAckMessage
    {
        public VoteKickDialogStyle DialogStyle { get; set; }
        public uint YesCount { get; set; }
        public uint PlayerVoted { get; set; }
        public VoteKickReason Reason { get; set; }
        public ulong Sender { get; set; }
        public ulong Target { get; set; }
    }
    [Packet(3066, PacketType.GameRule)]
    public class CaptainRoundCaptainLifeInfoAckMessage
    {
        public CaptainRoundCaptainLifeInfoAckMessage()
        {
            Players = Array.Empty<CaptainLifeDto>();
        }
        public CaptainRoundCaptainLifeInfoAckMessage(CaptainLifeDto[] players)
        {
            Players = players;
        }
        public CaptainLifeDto[] Players { get; set; }
    }
    [Packet(3067, PacketType.GameRule)]
    public class CaptainSubRoundWinAckMessage
    {
        public int Unk1 { get; set; }
        public Team Unk2 { get; set; }
        public CaptainSubRoundWinAckMessage()
        {
        }
        public CaptainSubRoundWinAckMessage(int Team, Team hasWon)
        {
            Unk1 = Team;
            Unk2 = hasWon;
        }
    }
    [Packet(3068, PacketType.GameRule)]
    public class CaptainCurrentRoundInfoAckMessage
    {
        public CaptainCurrentRoundInfoAckMessage()
        {
        }
        public CaptainCurrentRoundInfoAckMessage(int currentRound, TimeSpan roundTime)
        {
            CurrentRound = currentRound;
            RoundTime = roundTime;
        }
        public int CurrentRound { get; set; }
        [Sec] public TimeSpan RoundTime { get; set; }
    }
    [Packet(3069, PacketType.GameRule)]
    public class SeizeUpdateInfoAckMessage
    {
        public SeizeUpdateInfoAckMessage()
        {
        }
        public SeizeUpdateInfoAckMessage(SeizeUpdateInfoDto[] infos)
        {
            Infos = infos;
        }
        public SeizeUpdateInfoDto[] Infos { get; set; }
    }
    [Packet(3070, PacketType.GameRule)]
    public class SeizeUpdateInfoByIntrudeAckMessage
    {
        public SeizeUpdateInfoByIntrudeAckMessage()
        {
            Array.Empty<SeizeIntrudeInfoDto>();
        }
        public SeizeUpdateInfoByIntrudeAckMessage(SeizeIntrudeInfoDto[] infos)
        {
            Infos = infos;
        }
        public SeizeIntrudeInfoDto[] Infos { get; set; }
    }
    [Packet(3071, PacketType.GameRule)]
    public class SeizeFeverTimeAckMessage
    {
    }
    [Packet(3072, PacketType.GameRule)]
    public class SeizeBuffItemGainAckMessage
    {
        public ulong PickupID { get; set; }
        public ulong PlayerID { get; set; }
    }
    [Packet(3073, PacketType.GameRule)]
    public class SeizeDropBuffItemAckMessage
    {
        public ulong[] Pickups { get; set; }
    }
    [Packet(3085, PacketType.GameRule)]
    public class SeizeBuffApplyAckMessage
    {
        public ulong Player { get; set; }
        public uint DropSiteId { get; set; }
        public uint BuffData { get; set; }
    }
    [Packet(3074, PacketType.GameRule)]
    public class SeizeUpKeepScoreUpdateAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(3075, PacketType.GameRule)]
    public class SeizeUpKeepScoreGetAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(3076, PacketType.GameRule)]
    public class RoomChangeMasterReqeustAckMessage
    {
    }
    [Packet(3077, PacketType.GameRule)]
    public class RoomMixedTeamBriefingInfoAckMessage
    {
        public byte Unk1 { get; set; }
        public MixedTeamBriefingDto[] Unk2 { get; set; }
    }
    [Packet(3078, PacketType.GameRule)]
    public class GameEquipCheckAckMessage
    {
    }
    [Packet(3079, PacketType.GameRule)]
    public class RoomGameStartAckMessage
    {
    }
    [Packet(3080, PacketType.GameRule)]
    public class RoomGameLoadingAckMessage
    {
    }
    [Packet(3081, PacketType.GameRule)]
    public class GameTackUpdateAckMessage
    {
        public int Unk1 { get; set; }
        public short Unk2 { get; set; }
    }
    [Packet(3082, PacketType.GameRule)]
    public class RoomGameEndLoadingAckMessage
    {
        public RoomGameEndLoadingAckMessage()
        {
            Unk = 0;
        }
        public RoomGameEndLoadingAckMessage(ulong player)
        {
            Unk = player;
        }
        public ulong Unk { get; set; }
    }
    [Packet(3083, PacketType.GameRule)]
    public class RoomGamePlayCountDownAckMessage
    {
        public RoomGamePlayCountDownAckMessage()
        {
        }
        public RoomGamePlayCountDownAckMessage(TimeSpan timeinMs)
        {
            TimeinMs = timeinMs;
        }
        public TimeSpan TimeinMs { get; set; }
    }
    [Packet(3084, PacketType.GameRule)]
    public class InGameItemDropAckMessage
    {
        public ItemDropAckDto Item { get; set; }
    }
    [Packet(3085, PacketType.GameRule)]
    public class InGameItemGetAckMessage
    {
        public long Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(3086, PacketType.GameRule)]
    public class InGamePlayerResponseOfDeathAckMessage
    {
    }
    [Packet(3087, PacketType.GameRule)]
    public class ChallengeRankersAckMessage
    {
        public int Unk { get; set; }
        public ChallengeRankerDto[] Rankers { get; set; }
    }
    [Packet(3088, PacketType.GameRule)]
    public class ChallengeRankingListAckMessage
    {
        public int Unk { get; set; }
        public ChallengeRankerDto[] Rankers { get; set; }
    }
    [Packet(3107, PacketType.GameRule)]
    public class PromotionCointEventGetCoinAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(3089, PacketType.GameRule)]
    public class PromotionCouponEventIngameGetAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(3090, PacketType.GameRule)]
    public class RoomEnterPlayerForBookNameTagsAckMessage
    {
        public ulong AccountId { get; set; }
        public Team Team { get; set; }
        public PlayerGameMode PlayerGameMode { get; set; }
        public uint Exp { get; set; }
        public string Nickname { get; set; }
        public byte Unk2 { get; set; }
        public uint Unk1 { get; set; }
    }
    [Packet(3091, PacketType.GameRule)]
    public class RoomEnterPlayerInfoListForNameTagAckMessage
    {
        public RoomEnterPlayerInfoListForNameTagAckMessage()
        {
            Tags = Array.Empty<NameTagDto>();
        }
        public RoomEnterPlayerInfoListForNameTagAckMessage(NameTagDto[] tags)
        {
            Tags = tags;
        }
        public NameTagDto[] Tags { get; set; }
    }
    [SantanaContract]
    public class ScoreAIKillAssistAckMessage
    {
        [SantanaMember(0)]
        public LongPeerId Unk { get; set; }
    }
    [SantanaContract]
    public class Sync_First_Arena_Battle_Idx_Message
    {
        public Sync_First_Arena_Battle_Idx_Message()
        {
        }
        [SantanaMember(0)]
        public int Unk { get; set; }
        [SantanaMember(1)]
        public int Unk2 { get; set; }
        [SantanaMember(2)]
        public int Unk3 { get; set; }
        [SantanaMember(3)]
        public int Unk4 { get; set; }
        [SantanaMember(4)]
        public int Unk5 { get; set; }
        [SantanaMember(5)]
        public int Unk6 { get; set; }
    }
    [SantanaContract]
    public class Sync_Arena_Battle_Idx_Message
    {
        public Sync_Arena_Battle_Idx_Message()
        {
        }
        [SantanaMember(0)]
        public int Unk { get; set; }
        [SantanaMember(1, typeof(ArrayWithIntPrefixSerializer))]
        public int[] Unk2 { get; set; }
        [SantanaMember(2)]
        public int Unk3 { get; set; }
        [SantanaMember(3)]
        public ulong Unk4 { get; set; }
        [SantanaMember(4, typeof(ArrayWithIntPrefixSerializer))]
        public int[] Unk5 { get; set; }
        [SantanaMember(5)]
        public int Unk6 { get; set; }
        [SantanaMember(6)]
        public ulong Unk7 { get; set; }
    }
}
[Packet(3097, PacketType.GameRule)]
public class SyncArenaBattleIdxMessage
{
    public int Round { get; set; }
    public ArenaSyncDto[] AlphaSyncs { get; set; }
    public ArenaSyncDto[] BetaSyncs { get; set; }
    public SyncArenaBattleIdxMessage()
    {
    }
    public SyncArenaBattleIdxMessage(int round, ArenaSyncDto[] alphaSyncs, ArenaSyncDto[] betaSyncs)
    {
        Round = round;
        AlphaSyncs = alphaSyncs;
        BetaSyncs = betaSyncs;
    }
}
[Packet(3098, PacketType.GameRule)]
public class SyncFirstArenaBattleIdxMessage
{
    public ulong[] AlphaPlayers { get; set; }
    public ulong[] BetaPlayers { get; set; }
    public SyncFirstArenaBattleIdxMessage()
    {
        AlphaPlayers = Array.Empty<ulong>();
        BetaPlayers = Array.Empty<ulong>();
    }
    public SyncFirstArenaBattleIdxMessage(ulong[] alphaPlayers, ulong[] betaPlayers)
    {
        AlphaPlayers = alphaPlayers;
        BetaPlayers = betaPlayers;
    }
}
[Packet(3099, PacketType.GameRule)]
public class ScoreArenaDrawPlayMessage
{
}

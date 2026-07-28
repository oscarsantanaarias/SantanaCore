using System;
using SantanaLib.Serialization;
using Santana.Network.Data.GameRule;
using Santana.Network.Serializers;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.GameRule
{
    [Packet(2001, PacketType.GameRule)]
    public class RoomEnterPlayerReqMessage
    {
    }
    [Packet(2002, PacketType.GameRule)]
    public class RoomLeaveReguestReqMessage
    {
        public ulong AccountId { get; set; }
        public RoomLeaveReason Reason { get; set; }
    }
    [Packet(2003, PacketType.GameRule)]
    public class RoomTeamChangeReqMessage
    {
        public Team Team { get; set; }
        public PlayerGameMode Mode { get; set; }
    }
    [Packet(2004, PacketType.GameRule)]
    public class RoomAutoAssingTeamReqMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(2005, PacketType.GameRule)]
    public class RoomAutoMixingTeamReqMessage
    {
    }
    [Packet(2006, PacketType.GameRule)]
    public class RoomChoiceTeamChangeReqMessage
    {
        public ulong PlayerToMove { get; set; }
        public ulong PlayerToReplace { get; set; }
        public Team FromTeam { get; set; }
        public Team ToTeam { get; set; }
    }
    [Packet(2007, PacketType.GameRule)]
    public class GameEventMessageReqMessage
    {
        public GameEventMessage Event { get; set; }
        public ulong AccountId { get; set; }
        public uint Unk1 { get; set; }
        public ushort Value { get; set; }
        public uint Unk2 { get; set; }
    }
    [Packet(2008, PacketType.GameRule)]
    public class RoomReadyRoundReqMessage
    {
        public bool IsReady { get; set; }
    }
    [Packet(2009, PacketType.GameRule)]
    public class RoomBeginRoundReqMessage
    {
        public bool IsReady { get; set; }
    }
    [Packet(2010, PacketType.GameRule)]
    public class GameAvatarDurabilityDecreaseReqMessage
    {
    }
    [Packet(2011, PacketType.GameRule)]
    public class GameAvatarChangeReqMessage
    {
        public GameAvatarChangeReqMessage()
        {
            Unk1 = new ChangeAvatarUnk1Dto();
            Unk2 = Array.Empty<ChangeAvatarUnk2Dto>();
        }
        public ChangeAvatarUnk1Dto Unk1 { get; set; }
        public ChangeAvatarUnk2Dto[] Unk2 { get; set; }
    }
    [Packet(2012, PacketType.GameRule)]
    public class RoomChangeRuleNotifyReqMessage
    {
        public RoomChangeRuleNotifyReqMessage()
        {
            Settings = new ChangeRuleDto();
        }
        public ChangeRuleDto Settings { get; set; }
    }
    [Packet(2063, PacketType.GameRule)]
    public class RoomChangeRuleNotifyReq2Message
    {
        public RoomChangeRuleNotifyReq2Message()
        {
            Settings = new ChangeRuleDto2();
        }
        public ChangeRuleDto2 Settings { get; set; }
    }
    [Packet(2013, PacketType.GameRule)]
    public class ScoreMissionScoreReqMessage
    {
        public int Score { get; set; }
    }
    [Packet(2065, PacketType.GameRule)]
    public class ScoreAIKillReqMessage
    {
        public ulong[] Unk { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2066, PacketType.GameRule)]
    public class ArenaSetGameOptionReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(2014, PacketType.GameRule)]
    public class ScoreKillReqMessage
    {
        public ScoreDto Score { get; set; }
    }
    [Packet(2015, PacketType.GameRule)]
    public class ScoreKillAssistReqMessage
    {
        public ScoreAssist2Dto Score { get; set; }
    }
    [Packet(2016, PacketType.GameRule)]
    public class ScoreOffenseReqMessage
    {
        public Score2Dto Score { get; set; }
    }
    [Packet(2017, PacketType.GameRule)]
    public class ScoreOffenseAssistReqMessage
    {
        public ScoreAssist2Dto Score { get; set; }
    }
    [Packet(2018, PacketType.GameRule)]
    public class ScoreDefenseReqMessage
    {
        public Score2Dto Score { get; set; }
    }
    [Packet(2019, PacketType.GameRule)]
    public class ScoreDefenseAssistReqMessage
    {
        public ScoreAssist2Dto Score { get; set; }
    }
    [Packet(2020, PacketType.GameRule)]
    public class ScoreHealAssistReqMessage
    {
        public LongPeerId Id { get; set; }
    }
    [Packet(2021, PacketType.GameRule)]
    public class ScoreGoalReqMessage
    {
        public LongPeerId PeerId { get; set; }
    }
    [Packet(2022, PacketType.GameRule)]
    public class ScoreReboundReqMessage
    {
        public LongPeerId NewId { get; set; }
        public LongPeerId OldId { get; set; }
    }
    [Packet(2023, PacketType.GameRule)]
    public class ScoreSuicideReqMessage
    {
        public LongPeerId Id { get; set; }
        public uint Icon { get; set; }
    }
    [Packet(2024, PacketType.GameRule)]
    public class ScoreTeamKillReqMessage
    {
        public Score2Dto Score { get; set; }
    }
    [Packet(2025, PacketType.GameRule)]
    public class RoomItemChangeReqMessage
    {
        public RoomItemChangeReqMessage()
        {
            Unk1 = new ChangeItemsUnkDto();
            Unk2 = Array.Empty<ChangeAvatarUnk2Dto>();
        }
        public ChangeItemsUnkDto Unk1 { get; set; }
        public ChangeAvatarUnk2Dto[] Unk2 { get; set; }
    }
    [Packet(2026, PacketType.GameRule)]
    public class RoomPlayModeChangeReqMessage
    {
        public PlayerGameMode Mode { get; set; }
    }
    [Packet(2027, PacketType.GameRule)]
    public class ArcadeScoreSyncReqMessage
    {
        public ArcadeScoreSyncReqMessage()
        {
            Scores = Array.Empty<ArcadeScoreSyncDto>();
        }
        public ArcadeScoreSyncDto[] Scores { get; set; }
    }
    [Packet(2028, PacketType.GameRule)]
    public class ArcadeBeginRoundReqMessage
    {
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
    }


    [Packet(2029, PacketType.GameRule)]
    public class ArcadeStageClearReqMessage
    {
        public ArcadeStageClearReqMessage()
        {
            Scores = Array.Empty<ArcadeScoreSyncDto>();
        }
        public ArcadeScoreSyncDto[] Scores { get; set; }
    }


    [Packet(2030, PacketType.GameRule)]
    public class ArcadeStageFailedReqMessage
    {
        public ArcadeStageFailedReqMessage()
        {
            Scores = Array.Empty<ArcadeScoreSyncDto>();
        }
        public ArcadeScoreSyncDto[] Scores { get; set; }
    }
    [Packet(2031, PacketType.GameRule)]
    public class ArcadeStageInfoReqMessage
    {
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2032, PacketType.GameRule)]
    public class ArcadeEnablePlayTimeReqMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(2033, PacketType.GameRule)]
    public class ArcardRespawnReqMessage
    {
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2034, PacketType.GameRule)]
    public class ArcadeStageReadyReqMessage
    {
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2035, PacketType.GameRule)]
    public class ArcadeStageSelectReqMessage
    {
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
    }
    [Packet(2036, PacketType.GameRule)]
    public class SlaughterAttackPointReqMessage
    {
        public ulong AccountId { get; set; }
        public float Unk1 { get; set; }
        public float Unk2 { get; set; }
    }
    [Packet(2037, PacketType.GameRule)]
    public class SlaughterHealPointReqMessage
    {
        public float Unk { get; set; }
    }
    [Packet(2038, PacketType.GameRule)]
    public class ArcadeLoagdingSuccessReqMessage
    {
    }
    [Packet(2039, PacketType.GameRule)]
    public class MoneyUseCoinReqMessage
    {
        public BuffType BuffType { get; set; }
    }
    [Packet(2040, PacketType.GameRule)]
    public class LogBeginResponeReqMessage
    {
        public ulong Unk { get; set; }
    }
    [Packet(2041, PacketType.GameRule)]
    public class LogWeaponFireReqMessage
    {
        public ulong Unk1 { get; set; }
        public float Unk2 { get; set; }
        public int Unk3 { get; set; }
        public ulong Unk4 { get; set; }
        public int Unk5 { get; set; }
        public string Unk6 { get; set; }
        public int Unk7 { get; set; }
        public byte Unk8 { get; set; }
    }
    [Packet(2042, PacketType.GameRule)]
    public class GameKickOutRequestReqMessage
    {
        public ulong Sender { get; set; }
        public ulong Target { get; set; }
        public VoteKickReason Reason { get; set; }
    }
    [Packet(2043, PacketType.GameRule)]
    public class GameKickOutVoteResultReqMessage
    {
        public bool IsYes { get; set; }
    }
    [Packet(2044, PacketType.GameRule)]
    public class RoomIntrudeRoundReqMessage
    {
    }
    [Packet(2045, PacketType.GameRule)]
    public class GameLoadingSuccessReqMessage
    {
    }
    [Packet(2046, PacketType.GameRule)]
    public class SeizePositionCaptureReqMessage
    {
        public uint Base { get; set; }
        public byte IsCapturing { get; set; }
        public uint Distance { get; set; }
    }
    [Packet(2047, PacketType.GameRule)]
    public class SeizeBuffItemGainReqMessage
    {
        public ulong Item { get; set; }
    }
    [Packet(2048, PacketType.GameRule)]
    public class RoomChoiceMasterChangeReqMessage
    {
        public ulong AccountId { get; set; }
    }
    [Packet(2051, PacketType.GameRule)]
    public class InGameItemDropReqMessage
    {
        public ItemDropDto Item { get; set; }
    }
    [Packet(2052, PacketType.GameRule)]
    public class InGameItemGetReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(2053, PacketType.GameRule)]
    public class InGamePlayerResponseReqMessage
    {
    }
    [Packet(2050, PacketType.GameRule)]
    public class PromotionCointEventGetCoinReqMessage
    {
    }
    [Packet(2059, PacketType.GameRule)]
    public class RoomReadyRoundReq2Message
    {
    }
    [Packet(2060, PacketType.GameRule)]
    public class RoomBeginRoundReq2Message
    {
    }
    [Packet(2061, PacketType.GameRule)]
    public class RoomIntrudeRoundReq2Message
    {
    }
    [SantanaContract]
    public class GameEquipCheckReqMessage
    {
        [SantanaMember(0)] public EquipCheckDto Equip { get; set; }
    }
    [SantanaContract]
    public class Record_Burning_Data
    {
        [SantanaMember(0)]
        public uint unk1 { get; set; }
    }
    [SantanaContract]
    public class UseBurningBuff_Req
    {
        [SantanaMember(0)]
        public int unk1 { get; set; }
    }
    [SantanaContract]
    public class ChallengeRankingListReqMessage
    {
        [SantanaMember(0)] public int Unk { get; set; }
    }
    [SantanaContract]
    public class ChallengeResultReqMessage
    {
        [SantanaMember(0)] public ChallengeResultDto Item { get; set; }
    }
    [SantanaContract]
    public class ChallengeReStartReqMessage
    {
    }
    [SantanaContract]
    public class PromotionCouponEventIngameGetReqMessage
    {
        [SantanaMember(0)] public int Unk1 { get; set; }
        [SantanaMember(1)] public int Unk2 { get; set; }
    }
}

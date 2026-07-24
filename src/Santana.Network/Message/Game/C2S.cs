using System;
using SantanaLib.Serialization;
using Santana.Network.Serializers;
using ProudNetSrc.Serialization.Serializers;
using Santana.Network.Data.Game;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.Game
{
#if LATESTS4
    [Packet(3, PacketType.Game)]
#else
    [Packet(2, PacketType.Game)]
#endif
    public class CharacterCreateReqMessage
    {
        public byte Slot { get; set; }
        public CharacterStyle Style { get; set; }
    }
#if LATESTS4
    [Packet(4, PacketType.Game)]
#else
    [Packet(3, PacketType.Game)]
#endif
    public class CharacterSelectReqMessage
    {
        public byte Slot { get; set; }
    }
#if LATESTS4
    [Packet(5, PacketType.Game)]
#else
    [Packet(4, PacketType.Game)]
#endif
    public class CharacterDeleteReqMessage
    {
        public byte Slot { get; set; }
    }
#if LATESTS4
    [Packet(2, PacketType.Game)]
#else
    [Packet(5, PacketType.Game)]
#endif
    public class LoginRequestReqMessage
    {
        public uint Unk1 { get; set; }
        public string Username { get; set; }
        public Version Version { get; set; }
        public short Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public byte Unk4 { get; set; }
        public ulong AccountId { get; set; }
        public string SessionId { get; set; }
        public string Unk5 { get; set; }
        public bool KickConnection { get; set; }
        public string Unk6 { get; set; }
        public uint Unk7 { get; set; }
        public string Unk8 { get; set; }
        public string AuthToken { get; set; }
        public string newToken { get; set; }
        public string Datetime { get; set; }
    }
    [Packet(6, PacketType.Game)]
    public class RoomQuickStartReqMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(79, PacketType.Game)]
    public class RoomMakeReq2Message
    {
        public Santana.GameRule GameRule { get; set; }
        public byte MapId { get; set; }
        public byte PlayerLimit { get; set; }
        public short ScoreLimit { get; set; }
        public byte TimeLimit { get; set; }
        public int WeaponLimit { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool HasSpectator { get; set; }
        public byte SpectatorLimit { get; set; }
        public long Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte CreationId { get; set; }
        public int Unk4 { get; set; }
        public int FMBURNMode { get; set; }
#if LATESTS4
        public int ServerKey { get; set; }
#endif
    }
    [Packet(7, PacketType.Game)]
    public class RoomMakeReqMessage
    {
        public Santana.GameRule GameRule { get; set; }
        public byte MapId { get; set; }
        public byte PlayerLimit { get; set; }
        public short ScoreLimit { get; set; }
        public byte TimeLimit { get; set; }
        public int WeaponLimit { get; set; }
        public string Name { get; set; }
        public string Password { get; set; }
        public bool HasSpectator { get; set; }
        public byte SpectatorLimit { get; set; }
        public long Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
    }
    [Packet(8, PacketType.Game)]
    public class NickCheckReqMessage
    {
        public string Nickname { get; set; }
    }
    [Packet(9, PacketType.Game)]
    public class ItemUseItemReqMessage
    {
        public UseItemAction Action { get; set; }
        public byte CharacterSlot { get; set; }
        public byte EquipSlot { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(10, PacketType.Game)]
    public class RoomLeaveReqMessage
    {
        public RoomLeaveReason Reason { get; set; }
    }
    [Packet(11, PacketType.Game)]
    public class TimeSyncReqMessage
    {
        public uint Time { get; set; }
    }
    [Packet(12, PacketType.Game)]
    public class AdminShowWindowReqMessage
    {
    }
    [Packet(13, PacketType.Game)]
    public class ClubInfoReqMessage
    {
    }
    [Packet(14, PacketType.Game)]
    public class ChannelEnterReqMessage
    {
        public uint Channel { get; set; }
    }
    [Packet(15, PacketType.Game)]
    public class ChannelLeaveReqMessage
    {
        public uint Channel { get; set; }
    }
    [Packet(16, PacketType.Game)]
    public class ChannelInfoReqMessage
    {
        public ChannelInfoRequest Request { get; set; }
    }
    [Packet(17, PacketType.Game)]
    public class RoomEnterReqMessage
    {
        public uint RoomId { get; set; }
        public string Password { get; set; }
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
    }
    [Packet(18, PacketType.Game)]
    public class PlayerInfoReqMessage
    {
        public uint Unk { get; set; }
    }
    [Packet(19, PacketType.Game)]
    public class ItemBuyItemReqMessage
    {
        public ShopItemDto[] Items { get; set; }
    }
    [Packet(20, PacketType.Game)]
    public class ItemRepairItemReqMessage
    {
        public ulong[] Items { get; set; }
    }
    [Packet(21, PacketType.Game)]
    public class ItemRefundItemReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(22, PacketType.Game)]
    public class AdminActionReqMessage
    {
        public string Command { get; set; }
    }
    [Packet(23, PacketType.Game)]
    public class CharacterActiveEquipPresetReqMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(24, PacketType.Game)]
    public class LicenseGainReqMessage
    {
        public ItemLicense License { get; set; }
    }
    [Packet(25, PacketType.Game)]
    public class ClubNoticeChangeReqMessage
    {
        public string Unk { get; set; }
    }
    [Packet(26, PacketType.Game)]
    public class ClubInfoByIDReqMessage
    {
        public string Unk { get; set; }
    }
    [Packet(27, PacketType.Game)]
    public class ClubInfoByNameReqMessage
    {
        public string Unk { get; set; }
    }
    [Packet(28, PacketType.Game)]
    public class ItemInventoryInfoReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(29, PacketType.Game)]
    public class TaskNotifyReqMessage
    {
        public uint TaskId { get; set; }
        public ushort Progress { get; set; }
    }
    [Packet(30, PacketType.Game)]
    public class TaskReguestReqMessage
    {
        public byte Unk1 { get; set; }
        public uint TaskId { get; set; }
        public byte Unk2 { get; set; }
    }
    [Packet(31, PacketType.Game)]
    public class LicenseExerciseReqMessage
    {
        public ItemLicense License { get; set; }
    }
    [Packet(32, PacketType.Game)]
    public class ItemUseCoinReqMessage
    {
        public uint Unk { get; set; }
    }
    [Packet(33, PacketType.Game)]
    public class ItemUseEsperChipReqMessage
    {
        public ulong Unk1 { get; set; }
        public ulong Unk2 { get; set; }
    }
    [Packet(34, PacketType.Game)]
    public class PlayerBadUserReqMessage
    {
        public uint Unk { get; set; }
    }
    [Packet(35, PacketType.Game)]
    public class ClubJoinReqMessage
    {
        public uint ClubId { get; set; }
        public string ClubName { get; set; }
        public string Answer1 { get; set; }
        public string Answer2 { get; set; }
        public string Answer3 { get; set; }
        public string Answer4 { get; set; }
        public string Answer5 { get; set; }
    }
    [Packet(36, PacketType.Game)]
    public class ClubUnJoinReqMessage
    {
        public string Unk { get; set; }
    }
    [Packet(37, PacketType.Game)]
    public class NewShopUpdateCheckReqMessage
    {
        public string Date01 { get; set; }
        public string Date02 { get; set; }
        public string Date03 { get; set; }
        public string Date04 { get; set; }
        public uint Checksum01 { get; set; }
        public uint Checksum02 { get; set; }
        public uint Checksum03 { get; set; }
        public uint Checksum04 { get; set; }
    }
    [Packet(38, PacketType.Game)]
    public class ItemUseChangeNickReqMessage
    {
        public ulong ItemId { get; set; }
        public string Nickname { get; set; }
    }
    [Packet(39, PacketType.Game)]
    public class ItemUseRecordResetReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(40, PacketType.Game)]
    public class ItemUseCoinFillingReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(41, PacketType.Game)]
    public class PlayerFindInfoReqMessage
    {
        public string Nickname { get; set; }
    }
    [Packet(42, PacketType.Game)]
    public class ItemDiscardItemReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(43, PacketType.Game)]
    public class ItemUseCapsuleReqMessage
    {
        public ulong ItemId { get; set; }
    }
    [Packet(44, PacketType.Game)]
    public class ClubAddressReqMessage
    {
        public uint RequestId { get; set; }
        public uint LanguageId { get; set; }
        public uint Command { get; set; }
    }
    [Packet(45, PacketType.Game)]
    public class ClubHistoryReqMessage
    {
    }
    [Packet(46, PacketType.Game)]
    public class ItemUseChangeNickCancelReqMessage
    {
    }
    [Packet(47, PacketType.Game)]
    public class TutorialCompletedReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(48, PacketType.Game)]
    public class CharacterFirstCreateReqMessage
    {
        public string Nickname { get; set; }
        public int Gender { get; set; }
        [Fixed(8)]
        public ItemNumber[] FirstItems { get; set; }
    }
    [Packet(49, PacketType.Game)]
    public class ShoppingBasketActionReqMessage
    {
        public long Unk { get; set; }
        public ShopItemDto ShopItem { get; set; }
    }
    [Packet(50, PacketType.Game)]
    public class ShoppingBasketDeleteReqMessage
    {
        public long[] Unk { get; set; }
    }
    [Packet(51, PacketType.Game)]
    public class RandomShopUpdateCheckReqMessage
    {
        public string Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(52, PacketType.Game)]
    public class RandomShopRollingStartReqMessage
    {
        public int Category { get; set; }
    }
    [Packet(53, PacketType.Game)]
    public class RoomInfoRequestReqMessage
    {
        public uint RoomId { get; set; }
    }
    [Packet(54, PacketType.Game)]
    public class NoteGiftItemReqMessage
    {
        public ulong AccountId { get; set; }
        public string Nickname { get; set; }
        public string Receiver { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public ShopItemDto shopItem { get; set; }
        public long Unk7 { get; set; }
    }
    [Packet(55, PacketType.Game)]
    public class NoteImportuneItemReqMessage
    {
        public string Unk1 { get; set; }
        public long Unk2 { get; set; }
        public string Unk3 { get; set; }
        public string Unk4 { get; set; }
        public int Unk5 { get; set; }
        public ShopItemDto Unk6 { get; set; }
    }
    [Packet(56, PacketType.Game)]
    public class NoteGiftItemGainReqMessage
    {
        public long Unk { get; set; }
    }
    [Packet(57, PacketType.Game)]
    public class RoomQuickJoinReqMessage
    {
        public int GameRule { get; set; }
    }
    [Packet(58, PacketType.Game)]
    public class MoneyRefreshCashInfoReqMessage
    {
    }
    [Packet(59, PacketType.Game)]
    public class CardGambleReqMessage
    {
    }
    [Packet(80, PacketType.Game)]
    public class AlchemyCombinatioReqMessage
    {
        public AlchemyCombinatioReqMessage()
        {
            Info = Array.Empty<AlchemyDto>();
        }
        public int Id { get; set; }
        public AlchemyDto[] Info { get; set; }
    }
    [Packet(81, PacketType.Game)]
    public class AlchemyDecompositionReqMessage
    {
        public uint ID { get; set; }
        public uint Days { get; set; }
    }
    [Packet(60, PacketType.Game)]
    public class PromotionAttendanceGiftItemReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1196, PacketType.Game)]
    public class NewDailyAttendanceAckMessage
    {
        public uint Resultado { get; set; }
        public uint[] Items { get; set; }
        public byte Domingo { get; set; }
        public byte Lunes { get; set; }
        public byte Martes { get; set; }
        public byte Miercoles { get; set; }
        public byte Jueves { get; set; }
        public byte Viernes { get; set; }
        public byte Sabado { get; set; }
        public uint DiaActual { get; set; }
        public uint TotalReclamados { get; set; }
        public NewDailyAttendanceAckMessage()
        {
            Items = Array.Empty<uint>();
        }
        public NewDailyAttendanceAckMessage(uint resultado, uint[] items, byte domingo, byte lunes, byte martes, byte miercoles, byte jueves, byte viernes, byte sabado, uint diaactual, uint reclamados)
        {
            Resultado = resultado;
            Items = items;
            Domingo = domingo;
            Lunes = lunes;
            Martes = martes;
            Miercoles = miercoles;
            Jueves = jueves;
            Viernes = viernes;
            Sabado = sabado;
            DiaActual = diaactual;
            TotalReclamados = reclamados;
        }
    }
    public class DailyAttendanceRewardInfo
    {
        public int ItemIndex { get; set; }
        public int UserType { get; set; }
        public int Unknown2 { get; set; }
        public int Unknown3 { get; set; }
        public int DayOfWeek { get; set; }
        public uint ItemKey { get; set; }
        public uint ShopId { get; set; }
        public string PeriodType { get; set; }
        public int Period { get; set; }
        public int Color { get; set; }
        public uint EffectId { get; set; }
    }
    public class DailyAttendanceStateInfo
    {
        public uint[] Items { get; set; } = Array.Empty<uint>();
        public byte Domingo { get; set; }
        public byte Lunes { get; set; }
        public byte Martes { get; set; }
        public byte Miercoles { get; set; }
        public byte Jueves { get; set; }
        public byte Viernes { get; set; }
        public byte Sabado { get; set; }
        public uint DiaActual { get; set; }
        public uint TotalReclamados { get; set; }
    }
    [Packet(124, PacketType.Game)]
    public class NewCheckDailyAttendanceReqMessage
    {
    }
    [Packet(61, PacketType.Game)]
    public class PromotionCoinEventUseCoinReqMessage
    {
    }
    [Packet(130, PacketType.Game)]
    public class PromotionCoinEventDropCoinReqMessage
    {
    }
    [Packet(77, PacketType.Game)]
    public class CPromotionNewYearCardUseReqMessage
    {
        public int ID { get; set; }
    }
    [Packet(62, PacketType.Game)]
    public class ItemEnchanReqMessage
    {
        public long ItemId { get; set; }
        public long Unk2 { get; set; }
    }
    [Packet(71, PacketType.Game)]
    public class ItemMPRefillReqMessage
    {
        public ulong MPItemId { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(121, PacketType.Game)]
    public class UseEnchantChipReqMessage
    {
        public ulong ChipId { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(122, PacketType.Game)]
    public class MoveEnchantChipReqMessage
    {
        public ulong ChipId { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(72, PacketType.Game)]
    public class UseInstantItemRemoveEffectReqMessage
    {
        public ulong DelItemId { get; set; }
        public ulong ItemId { get; set; }
        public uint EffectId { get; set; }
    }
    [Packet(63, PacketType.Game)]
    public class CPromotionCardShuffleReqMessage
    {
    }
    [Packet(64, PacketType.Game)]
    public class BillingCashInfoReqMessage
    {
    }
    [Packet(66, PacketType.Game)]
    public class PromotionCouponEventReqMessage
    {
        public long Unk { get; set; }
    }
    [Packet(89, PacketType.Game)]
    public class Btc_Clear_ReqMessage
    {
        public int Mode { get; set; }
        public int Index { get; set; }
    }
    [Packet(123, PacketType.Game)]
    public class CheckHashKeyValueReqMessage
    {
        public string ResourceHash { get; set; }
    }
    [Packet(104, PacketType.Game)]
    public class ClubNoticePointRefreshReqMessage
    {
    }
    [Packet(94, PacketType.Game)]
    public class Match_Start_Req
    {
    }
    [Packet(95, PacketType.Game)]
    public class Match_Stop_Req
    {
    }
    [Packet(96, PacketType.Game)]
    public class Match_List_Req
    {
    }
    [Packet(97, PacketType.Game)]
    public class Match_Invite_Req
    {
        public int Unk1 { get; set; }
    }
    [Packet(98, PacketType.Game)]
    public class Battle_Invites_Received_Result
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(99, PacketType.Game)]
    public class ReMatchReqMessage
    {
        public int Id { get; set; }
    }
    [Packet(100, PacketType.Game)]
    public class MatchVoteBeginReqMessage
    {
    }
    [Packet(101, PacketType.Game)]
    public class MatchClubMarkReqMessage
    {
    }
    [Packet(102, PacketType.Game)]
    public class MatchPointReqMessage
    {
    }
    [Packet(103, PacketType.Game)]
    public class MatchRoomQuit_Req
    {
    }
    [Packet(106, PacketType.Game)]
    public class ClubSearchRoomReqMessage
    {
        public byte Unk1 { get; set; }
    }
    [Packet(107, PacketType.Game)]
    public class Club_Stadium_Edit_MapData_Req
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(108, PacketType.Game)]
    public class Club_Stadium_Edit_Blastinfo_Edit_req
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public byte Unk4 { get; set; }
    }
    [Packet(109, PacketType.Game)]
    public class Club_Stadium_Info_Req
    {
    }
    [Packet(105, PacketType.Game)]
    public class ClubNoticeRecordRefreshReqMessage
    {
    }
    [Packet(118, PacketType.Game)]
    public class UnionMainUiReqMessage
    {
    }
    [Packet(119, PacketType.Game)]
    public class UnionSearchRoomReqMessage
    {
        public byte Unk1 { get; set; }
    }
    [Packet(87, PacketType.Game)]
    public class AchieveMissionReqMessage
    {
    }
    [Packet(88, PacketType.Game)]
    public class AchieveMissionRewardReqMessage
    {
        public int MissionId { get; set; }
        public int RewardId { get; set; }
    }
    [Packet(85, PacketType.Game)]
    public class DailyMissionResetReqMessage
    {
        public byte unk1 { get; set; }
        public byte unk2 { get; set; }
        public byte unk3 { get; set; }
    }
    [Packet(86, PacketType.Game)]
    public class DailyMissionNextStepReqMessage
    {
        public DailyMissionNextStepReqMessage()
        {
            unk1 = 0;
        }
        public int unk1 { get; set; }
        public int unk2 { get; set; }
    }
    [Packet(111, PacketType.Game)]
    public class ClubOtherClubinfoReqMessage
    {
        public string ClubName { get; set; }
    }
    [Packet(84, PacketType.Game)]
    public class Daily_Mission_Reward_ReqMessage
    {
        public int unk1 { get; set; }
    }
    [Packet(93, PacketType.Game)]
    public class EsperEnchantReqMessage
    {
        public ulong EsperItemId { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(120, PacketType.Game)]
    public class EsperEnchantPercentUpReqMessage
    {
        public ulong EsperItemId { get; set; }
        public ulong PlusChips { get; set; }
    }
    [Packet(127, PacketType.Game)]
    public class BattleyeC2SDataMessage
    {
        public BattleyeC2SDataMessage()
        {
            DataSize = 0;
            Data = Array.Empty<byte>();
        }
        public uint DataSize { get; set; }
        [Scalar] public byte[] Data { get; set; }
    }
    [Dto]
    public class CollectBookItem
    {
        public uint Key { get; set; }
        public int ItemId { get; set; }
        public byte Color { get; set; }
    }
    [Packet(67, PacketType.Game)]
    public class CollectBook_UpdateCheck_Req
    {
        public string Value { get; set; }
    }
    [Packet(68, PacketType.Game)]
    public class CollectBook_InventoryInfo_Req
    {
    }
    [Packet(69, PacketType.Game)]
    public class CollectB_ItemRegisterReq
    {
        public CollectBookItem[] Items { get; set; }
    }
    [Packet(70, PacketType.Game)]
    public class CollectBook_UseReward_Req
    {
        public ulong Unk1 { get; set; }
    }
    [Packet(73, PacketType.Game)]
    public class Promotion_RouletteMachine_Start_Req
    {
    }
}

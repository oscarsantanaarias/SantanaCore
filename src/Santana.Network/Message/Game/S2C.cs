using SantanaLib;
using SantanaLib.Serialization;
using DotNetty.Common.Internal;
using Santana;
using Santana.Network.Data.Game;
using Santana.Network.Message.Club;
using Santana.Network.Message.GameRule;
using Santana.Network.Serializers;
using ProudNetSrc.Serialization.Serializers;
using System;
using System.Collections.Generic;
using System.Net;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.Game
{
    [Packet(1001, PacketType.Game)]
    public class LoginReguestAckMessage
    {
        public LoginReguestAckMessage()
        {
            Unk1 = "";
            Unk2 = "";
            ServerTime = DateTimeOffset.Now;
        }
        public LoginReguestAckMessage(GameLoginResult result, ulong accountId)
            : this()
        {
            AccountId = accountId;
            Result = result;
        }
        public LoginReguestAckMessage(GameLoginResult result)
            : this()
        {
            Result = result;
        }
        public ulong AccountId { get; set; }
        public GameLoginResult Result { get; set; }
        public DateTimeOffset ServerTime { get; set; }
        public string Unk1 { get; set; }
        public string Unk2 { get; set; }
    }
    [Packet(1002, PacketType.Game)]
    public class PlayerAccountInfoAckMessage
    {
        public PlayerAccountInfoAckMessage()
        {
            Info = new PlayerAccountInfoDto();
        }
        public PlayerAccountInfoAckMessage(PlayerAccountInfoDto info)
        {
            Info = info;
        }
        public PlayerAccountInfoDto Info { get; set; }
    }
    [Packet(1003, PacketType.Game)]
    public class CharacterCurrentInfoAckMessage
    {
        public CharacterCurrentInfoAckMessage()
        {
            Unk1 = 1;
            Unk2 = 3;
        }
        public byte Slot { get; set; }
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public CharacterStyle Style { get; set; }
    }
    [Packet(1004, PacketType.Game)]
    public class CharacterCurrentItemInfoAckMessage
    {
        public CharacterCurrentItemInfoAckMessage()
        {
            Weapons = new ulong[9];
            Skills = new ulong[1];
            Clothes = new ulong[7];
        }
        public byte Slot { get; set; }
        [Index] public ulong[] Weapons { get; set; }
        [Index] public ulong[] Skills { get; set; }
        [Index] public ulong[] Clothes { get; set; }
    }
    [Packet(1005, PacketType.Game)]
    public class ItemInventoryInfoAckMessage
    {
        public ItemInventoryInfoAckMessage()
        {
            Items = Array.Empty<ItemDto>();
        }
        public ItemInventoryInfoAckMessage(ItemDto[] items)
        {
            Items = items;
        }
        public ItemDto[] Items { get; set; }
    }
    [Packet(1006, PacketType.Game)]
    public class CharacterDeleteAckMessage
    {
        public CharacterDeleteAckMessage()
        {
        }
        public CharacterDeleteAckMessage(byte slot)
        {
            Slot = slot;
        }
        public byte Slot { get; set; }
    }
    [Packet(1007, PacketType.Game)]
    public class CharacterSelectAckMessage
    {
        public CharacterSelectAckMessage()
        {
        }
        public CharacterSelectAckMessage(byte slot)
        {
            Slot = slot;
        }
        public byte Slot { get; set; }
    }
    [Packet(1008, PacketType.Game)]
    public class CSuccessCreateCharacterAckMessage
    {
        public CSuccessCreateCharacterAckMessage()
        {
            MaxSkills = 1;
            MaxWeapons = 3;
        }
        public CSuccessCreateCharacterAckMessage(byte slot, CharacterStyle style)
            : this()
        {
            Slot = slot;
            Style = style;
        }
        public byte Slot { get; set; }
        public CharacterStyle Style { get; set; }
        public byte MaxSkills { get; set; }
        public byte MaxWeapons { get; set; }
    }
    [Packet(1009, PacketType.Game)]
    public class ServerResultAckMessage
    {
        public ServerResultAckMessage()
        {
        }
        public ServerResultAckMessage(ServerResult result)
        {
            Result = result;
        }
        public ServerResult Result { get; set; }
    }
    [Packet(1010, PacketType.Game)]
    public class NickCheckAckMessage
    {
        public NickCheckAckMessage()
        {
        }
        public NickCheckAckMessage(bool isTaken)
        {
            IsTaken = isTaken;
        }
        [IntBool] public bool IsTaken { get; set; }
    }
    [Packet(1011, PacketType.Game)]
    public class ItemUseItemAckMessage
    {
        public ItemUseItemAckMessage()
        {
        }
        public ItemUseItemAckMessage(UseItemAction action, byte characterSlot, byte equipSlot, ulong itemId)
        {
            CharacterSlot = characterSlot;
            EquipSlot = equipSlot;
            ItemId = itemId;
            Action = action;
        }
        public byte CharacterSlot { get; set; }
        public byte EquipSlot { get; set; }
        public ulong ItemId { get; set; }
        public UseItemAction Action { get; set; }
    }
    [Packet(1012, PacketType.Game)]
    public class ItemUpdateInventoryAckMessage
    {
        public ItemUpdateInventoryAckMessage()
        {
            Item = new ItemDto();
        }
        public ItemUpdateInventoryAckMessage(InventoryAction action, ItemDto item)
        {
            Action = action;
            Item = item;
        }
        public InventoryAction Action { get; set; }
        public ItemDto Item { get; set; }
    }
    [Packet(1013, PacketType.Game)]
    public class RoomCurrentCharacterSlotAckMessage
    {
        public RoomCurrentCharacterSlotAckMessage()
        {
        }
        public RoomCurrentCharacterSlotAckMessage(uint unk, byte slot)
        {
            Unk = unk;
            Slot = slot;
        }
        public uint Unk { get; set; }
        public byte Slot { get; set; }
    }
    [Packet(1014, PacketType.Game)]
    public class RoomEnterPlayerInfoAckMessage
    {
        public RoomEnterPlayerInfoAckMessage()
        {
            Player = new RoomPlayerDto();
        }
        public RoomEnterPlayerInfoAckMessage(RoomPlayerDto plr)
        {
            Player = plr;
        }
        public RoomPlayerDto Player { get; set; }
    }
    [Packet(1015, PacketType.Game)]
    public class RoomEnterClubInfoAckMessage
    {
        public RoomEnterClubInfoAckMessage()
        {
            Club = new PlayerClubInfoDto();
        }
        public RoomEnterClubInfoAckMessage(PlayerClubInfoDto club)
        {
            Club = club;
        }
        public PlayerClubInfoDto Club { get; set; }
    }
    [Packet(1016, PacketType.Game)]
    public class RoomPlayerInfoListForEnterPlayerAckMessage
    {
        public RoomPlayerInfoListForEnterPlayerAckMessage()
        {
            Players = Array.Empty<RoomPlayerDto>();
        }
        public RoomPlayerInfoListForEnterPlayerAckMessage(RoomPlayerDto[] players)
        {
            Players = players;
        }
        public RoomPlayerDto[] Players { get; set; }
    }
    [Packet(1119, PacketType.Game)]
    public class RoomPlayerInfoListForEnterPlayerForCollectBookAckMessage
    {
        public RoomPlayerInfoListForEnterPlayerForCollectBookAckMessage()
        {
            Count = 0;
        }
        public int Count { get; set; }
    }
    [Packet(1017, PacketType.Game)]
    public class RoomClubInfoListForEnterPlayerAckMessage
    {
        public RoomClubInfoListForEnterPlayerAckMessage()
        {
            Clubs = Array.Empty<PlayerClubInfoDto>();
        }
        public RoomClubInfoListForEnterPlayerAckMessage(PlayerClubInfoDto[] infos)
        {
            Clubs = infos;
        }
        public PlayerClubInfoDto[] Clubs { get; set; }
    }
    [Packet(1132, PacketType.Game)]
    public class RoomEnterRoomInfoAck2Message
    {
        public RoomEnterRoomInfoAck2Message()
        {
            RelayEndPoint = new IPEndPoint(0, 0);
        }
        public uint RoomId { get; set; }
        public Santana.GameRule GameRule { get; set; }
        public byte MapId { get; set; }
        public byte PlayerLimit { get; set; }
        public GameState GameState { get; set; }
        public GameTimeState GameTimeState { get; set; }
        public uint TimeLimit { get; set; }
        public uint Unk1 { get; set; }
        public uint TimeSync { get; set; }
        public uint ScoreLimit { get; set; }
        public byte Unk2 { get; set; }
        [EndpointStr] public IPEndPoint RelayEndPoint { get; set; }
        public bool CreatedRoom { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public byte LastMapId { get; set; }
    }
    [Packet(1018, PacketType.Game)]
    public class RoomEnterRoomInfoAckMessage
    {
        public RoomEnterRoomInfoAckMessage()
        {
            RelayEndPoint = new IPEndPoint(0, 0);
        }
        public uint RoomId { get; set; }
        public Santana.GameRule GameRule { get; set; }
        public byte MapId { get; set; }
        public byte PlayerLimit { get; set; }
        public GameState GameState { get; set; }
        public GameTimeState GameTimeState { get; set; }
        public uint TimeLimit { get; set; }
        public uint Unk1 { get; set; }
        public uint TimeSync { get; set; }
        public uint ScoreLimit { get; set; }
        public byte Unk2 { get; set; }
        [EndpointStr] public IPEndPoint RelayEndPoint { get; set; }
        public bool CreatedRoom { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public byte LastMapId { get; set; }
    }
    [Packet(1019, PacketType.Game)]
    public class RoomLeavePlayerInfoAckMessage
    {
        public RoomLeavePlayerInfoAckMessage()
        {
        }
        public RoomLeavePlayerInfoAckMessage(ulong accountId)
        {
            AccountId = accountId;
        }
        public ulong AccountId { get; set; }
    }
    [Packet(1020, PacketType.Game)]
    public class TimeSyncAckMessage
    {
        public uint ClientTime { get; set; }
        public uint ServerTime { get; set; }
    }
    [Packet(1021, PacketType.Game)]
    public class RoomChangeRoomInfoAckMessage
    {
        public RoomChangeRoomInfoAckMessage()
        {
            Room = new RoomDto();
        }
        public RoomChangeRoomInfoAckMessage(RoomDto room)
        {
            Room = room;
        }
        public RoomDto Room { get; set; }
    }
    [Packet(1131, PacketType.Game)]
    public class RoomChangeRoomInfoAck2Message
    {
        public RoomChangeRoomInfoAck2Message()
        {
            Room = new RoomDto();
        }
        public RoomChangeRoomInfoAck2Message(RoomDto room)
        {
            Room = room;
        }
        public RoomDto Room { get; set; }
    }
    [Packet(1022, PacketType.Game)]
    public class NewShopUpdateEndAckMessage
    {
    }
    [Packet(1023, PacketType.Game)]
    public class ChannelListInfoAckMessage
    {
        public ChannelListInfoAckMessage()
        {
            Channels = Array.Empty<ChannelInfoDto>();
        }
        public ChannelListInfoAckMessage(ChannelInfoDto[] channels)
        {
            Channels = channels;
        }
        public ChannelInfoDto[] Channels { get; set; }
    }
    [Packet(1135, PacketType.Game)]
    public class RoomDeployAck2Message
    {
        public RoomDeployAck2Message()
        {
            Room = new RoomDto();
        }
        public RoomDeployAck2Message(RoomDto room)
        {
            Room = room;
        }
        public RoomDto Room { get; set; }
    }
    [Packet(1024, PacketType.Game)]
    public class RoomDeployAckMessage
    {
        public RoomDeployAckMessage()
        {
            Room = new RoomDto();
        }
        public RoomDeployAckMessage(RoomDto room)
        {
            Room = room;
        }
        public RoomDto Room { get; set; }
    }
    [Packet(1025, PacketType.Game)]
    public class RoomDisposeAckMessage
    {
        public RoomDisposeAckMessage()
        {
        }
        public RoomDisposeAckMessage(uint roomId)
        {
            RoomId = roomId;
        }
        public uint RoomId { get; set; }
    }
    [Packet(1026, PacketType.Game)]
    public class GamePlayerInfoAckMessage
    {
        public ulong AccountID { get; set; }
        public string Nickname { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
    }
    [Packet(1027, PacketType.Game)]
    public class ItemBuyItemAckMessage
    {
        public ItemBuyItemAckMessage()
        {
            Ids = Array.Empty<ulong>();
            Item = new ShopItemDto();
        }
        public ItemBuyItemAckMessage(ItemBuyResult result)
            : this()
        {
            Result = result;
        }
        public ItemBuyItemAckMessage(ulong[] ids, ShopItemDto item)
        {
            Ids = ids;
            Result = ItemBuyResult.OK;
            Item = item;
        }
        public ulong[] Ids { get; set; }
        public ItemBuyResult Result { get; set; }
        public ShopItemDto Item { get; set; }
    }
    [Packet(1028, PacketType.Game)]
    public class ItemRepairItemAckMessage
    {
        public ItemRepairResult Result { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(1029, PacketType.Game)]
    public class ItemDurabilityItemAckMessage
    {
        public ItemDurabilityItemAckMessage()
        {
            Items = Array.Empty<ItemDurabilityInfoDto>();
        }
        public ItemDurabilityItemAckMessage(ItemDurabilityInfoDto[] items)
        {
            Items = items;
        }
        public ItemDurabilityInfoDto[] Items { get; set; }
    }
    [Packet(1030, PacketType.Game)]
    public class ItemRefundItemAckMessage
    {
        public ulong ItemId { get; set; }
        public ItemRefundResult Result { get; set; }
    }
    [Packet(1031, PacketType.Game)]
    public class MoneyRefreshCashInfoAckMessage
    {
        public MoneyRefreshCashInfoAckMessage()
        {
        }
        public MoneyRefreshCashInfoAckMessage(uint pen, uint ap)
        {
            PEN = pen;
            AP = ap;
        }
        public uint PEN { get; set; }
        public uint AP { get; set; }
    }
    [Packet(1032, PacketType.Game)]
    public class AdminActionAckMessage
    {
        public AdminActionAckMessage()
        {
            Message = "";
        }
        public byte Result { get; set; }
        public string Message { get; set; }
    }
    [Packet(1033, PacketType.Game)]
    public class AdminShowWindowAckMessage
    {
        public AdminShowWindowAckMessage()
        {
        }
        public AdminShowWindowAckMessage(bool disableConsole)
        {
            DisableConsole = disableConsole;
        }
        public bool DisableConsole { get; set; }
    }
    [Packet(1034, PacketType.Game)]
    public class NoticeAdminMessageAckMessage
    {
        public NoticeAdminMessageAckMessage()
        {
            Message = "";
        }
        public NoticeAdminMessageAckMessage(string message)
        {
            Message = message;
        }
        public string Message { get; set; }
    }
    [Packet(1035, PacketType.Game)]
    public class CharacterCurrentSlotInfoAckMessage
    {
        public byte CharacterCount { get; set; }
        public byte MaxSlots { get; set; }
        public byte ActiveCharacter { get; set; }
    }
    [Packet(1036, PacketType.Game)]
    public class ItemRefreshInvalidEquipItemAckMessage
    {
        public ItemRefreshInvalidEquipItemAckMessage()
        {
            Items = Array.Empty<ulong>();
        }
        public ulong[] Items { get; set; }
    }
    [Packet(1037, PacketType.Game)]
    public class ItemClearInvalidEquipItemAckMessage
    {
        public ItemClearInvalidEquipItemAckMessage()
        {
            Items = Array.Empty<InvalidateItemInfoDto>();
        }
        public InvalidateItemInfoDto[] Items { get; set; }
    }
    [Packet(1038, PacketType.Game)]
    public class CharacterAvatarEquipPresetAckMessage
    {
        public byte Unk { get; set; }
    }
    [Packet(1039, PacketType.Game)]
    public class LicenseMyInfoAckMessage
    {
        public LicenseMyInfoAckMessage()
        {
            Licenses = Array.Empty<uint>();
        }
        public LicenseMyInfoAckMessage(uint[] licenses)
        {
            Licenses = licenses;
        }
        public uint[] Licenses { get; set; }
    }
    [Packet(1040, PacketType.Game)]
    public class ClubInfoAckMessage
    {
        public ClubInfoAckMessage()
        {
            ClubInfo = new PlayerClubInfoDto();
        }
        public ClubInfoAckMessage(PlayerClubInfoDto clubInfo)
        {
            ClubInfo = clubInfo;
        }
        public PlayerClubInfoDto ClubInfo { get; set; }
    }
    [Packet(1041, PacketType.Game)]
    public class ClubHistoryAckMessage
    {
        public ClubHistoryAckMessage()
        {
            History = new ClubHistoryDto();
        }
        public ClubHistoryDto History { get; set; }
    }
    [Packet(1042, PacketType.Game)]
    public class ItemEquipBoostItemInfoAckMessage
    {
        public ItemEquipBoostItemInfoAckMessage()
        {
            Items = Array.Empty<ulong>();
        }
        public ItemEquipBoostItemInfoAckMessage(ulong[] items)
        {
            Items = items;
        }
        public ulong[] Items { get; set; }
    }
    [Packet(1043, PacketType.Game)]
    public class ClubFindInfoAckMessage
    {
    }
    [Packet(1044, PacketType.Game)]
    public class TaskInfoAckMessage
    {
        public TaskInfoAckMessage()
        {
            Tasks = Array.Empty<TaskDto>();
        }
        public TaskDto[] Tasks { get; set; }
    }
    [Packet(1045, PacketType.Game)]
    public class TaskUpdateAckMessage
    {
        public uint TaskId { get; set; }
        public ushort Progress { get; set; }
    }
    [Packet(1046, PacketType.Game)]
    public class TaskRequestAckMessage
    {
        public uint TaskId { get; set; }
        public MissionRewardType RewardType { get; set; }
        public uint Reward { get; set; }
        public byte Slot { get; set; }
    }
    [Packet(1047, PacketType.Game)]
    public class TaskRemoveAckMessage
    {
        public uint TaskId { get; set; }
    }
    [Packet(1048, PacketType.Game)]
    public class MoenyRefreshCoinInfoAckMessage
    {
        public uint ArcadeCoins { get; set; }
        public uint BuffCoins { get; set; }
        public MoenyRefreshCoinInfoAckMessage(uint arcadeCoins, uint buffCoins)
        {
            ArcadeCoins = arcadeCoins;
            BuffCoins = buffCoins;
        }
        public MoenyRefreshCoinInfoAckMessage()
        {
        }
    }
    [Packet(1049, PacketType.Game)]
    public class ItemUseEsperChipItemAckMessage
    {
        public int Unk1 { get; set; }
        public long Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(1050, PacketType.Game)]
    public class RequitalArcadeRewardAckMessage
    {
        public ArcadeRewardDto Reward { get; set; }
    }
    [Packet(1051, PacketType.Game)]
    public class PlayeArcadeMapInfoAckMessage
    {
        public PlayeArcadeMapInfoAckMessage()
        {
            Infos = Array.Empty<ArcadeMapInfoDto>();
        }
        public ArcadeMapInfoDto[] Infos { get; set; }
    }
    [Packet(1052, PacketType.Game)]
    public class PlayerArcadeStageInfoAckMessage
    {
        public PlayerArcadeStageInfoAckMessage()
        {
            Infos = Array.Empty<ArcadeStageInfoDto>();
        }
        public ArcadeStageInfoDto[] Infos { get; set; }
    }
    [Packet(1053, PacketType.Game)]
    public class MoneyRefreshPenInfoAckMessage
    {
        public uint Unk { get; set; }
    }
    [Packet(1054, PacketType.Game)]
    public class ItemUseCapsuleAckMessage
    {
        public ItemUseCapsuleAckMessage()
        {
            Rewards = Array.Empty<CapsuleRewardDto>();
        }
        public ItemUseCapsuleAckMessage(byte result)
            : this()
        {
            Result = result;
        }
        public ItemUseCapsuleAckMessage(CapsuleRewardDto[] rewards, byte result)
        {
            Rewards = rewards;
            Result = result;
        }
        public CapsuleRewardDto[] Rewards { get; set; }
        public byte Result { get; set; }
    }
    [Packet(1055, PacketType.Game)]
    public class AdminHGWKickAckMessage
    {
        public AdminHGWKickAckMessage()
        {
            Message = "";
        }
        public string Message { get; set; }
    }
    [Packet(1056, PacketType.Game)]
    public class GameClubJoinAckMessage
    {
        public GameClubJoinAckMessage()
        {
            Message = "";
        }
        public uint Unk { get; set; }
        public string Message { get; set; }
    }
    [Packet(1057, PacketType.Game)]
    public class ClubUnJoinAckMessage
    {
        public ClubUnJoinAckMessage()
        {
        }
        public ClubUnJoinAckMessage(uint result)
        {
            Result = result;
        }
        public uint Result { get; set; }
    }
    [Packet(1058, PacketType.Game)]
    public class NewShopUpdateCheckAckMessage
    {
        public NewShopUpdateCheckAckMessage()
        {
            Date01 = "";
            Date02 = "";
            Date03 = "";
            Date04 = "";
        }
        public uint Unk { get; set; }
        public string Date01 { get; set; }
        public string Date02 { get; set; }
        public string Date03 { get; set; }
        public string Date04 { get; set; }
    }
    [Packet(1059, PacketType.Game)]
    public class NewShopUpdataInfoAckMessage
    {
        public NewShopUpdataInfoAckMessage()
        {
            Data = Array.Empty<byte>();
            Date = "";
        }
        public ShopResourceType Type { get; set; }
        [Scalar] public byte[] Data { get; set; }
        public int DataLength { get; set; }
        public uint Unk2 { get; set; }
        public string Date { get; set; }
    }
    [Packet(1060, PacketType.Game)]
    public class ItemUseChangeNickAckMessage
    {
        public ItemUseChangeNickAckMessage()
        {
            Unk3 = "";
        }
        public uint Result { get; set; }
        public ulong Unk2 { get; set; }
        public string Unk3 { get; set; }
    }
    [Packet(1061, PacketType.Game)]
    public class ItemUseRecordResetAckMessage
    {
        public uint Result { get; set; }
        public ulong Unk2 { get; set; }
    }
    [Packet(1062, PacketType.Game)]
    public class ItemUseCoinFillingAckMessage
    {
        public uint Result { get; set; }
    }
    [Packet(1063, PacketType.Game)]
    public class PlayerFindInfoAckMessage
    {
        public ulong AccountId { get; set; }
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
        public string Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
    }
    [Packet(1064, PacketType.Game)]
    public class ItemDiscardItemAckMessage
    {
        public uint Result { get; set; }
        public ulong ItemId { get; set; }
    }
    [Packet(1065, PacketType.Game)]
    public class ItemInventroyDeleteAckMessage
    {
        public ItemInventroyDeleteAckMessage()
        {
        }
        public ItemInventroyDeleteAckMessage(ulong itemId)
        {
            ItemId = itemId;
        }
        public ulong ItemId { get; set; }
    }
    [Packet(1066, PacketType.Game)]
    public class ClubAddressAckMessage
    {
        public ClubAddressAckMessage()
        {
            Fingerprint = "";
        }
        public ClubAddressAckMessage(string fingerprint, uint unk2)
        {
            Fingerprint = fingerprint;
            Unk2 = unk2;
        }
        public string Fingerprint { get; set; }
        public uint Unk2 { get; set; }
    }
    [Packet(1067, PacketType.Game)]
    public class ItemUseChangeNickCancelAckMessage
    {
        public ItemUseChangeNickCancelAckMessage()
        {
        }
        public ItemUseChangeNickCancelAckMessage(uint result)
        {
            Result = result;
        }
        public uint Result { get; set; }
    }
    [Packet(1068, PacketType.Game)]
    public class RequitalEventItemRewardAckMessage
    {
        public uint Unk1 { get; set; }
        public uint Unk2 { get; set; }
        public uint Unk3 { get; set; }
        public uint Unk4 { get; set; }
        public uint Unk5 { get; set; }
        public uint Unk6 { get; set; }
        public uint Unk7 { get; set; }
    }
    [Packet(1134, PacketType.Game)]
    public class RoomListInfoAck2Message
    {
        public RoomListInfoAck2Message()
        {
            Rooms = Array.Empty<RoomDto>();
        }
        public RoomListInfoAck2Message(RoomDto[] rooms)
        {
            Rooms = rooms;
        }
        public RoomDto[] Rooms { get; set; }
    }
    [Packet(1069, PacketType.Game)]
    public class RoomListInfoAckMessage
    {
        public RoomListInfoAckMessage()
        {
            Rooms = Array.Empty<RoomDto>();
        }
        public RoomListInfoAckMessage(RoomDto[] rooms)
        {
            Rooms = rooms;
        }
        public RoomDto[] Rooms { get; set; }
    }
    [Packet(1070, PacketType.Game)]
    public class NickDefaultAckMessage
    {
        public string Unk { get; set; }
    }
    [Packet(1071, PacketType.Game)]
    public class RequitalGiveItemResultAckMessage
    {
        public RequitalGiveItemResultAckMessage()
        {
            Items = Array.Empty<RequitalGiveItemResultDto>();
        }
        public RequitalGiveItemResultAckMessage(RequitalGiveItemResultDto[] items)
        {
            Items = items;
        }
        public RequitalGiveItemResultDto[] Items { get; set; }
    }
    [Packet(1072, PacketType.Game)]
    public class ShoppingBasketActionAckMessage
    {
        public ShoppingBasketActionAckMessage()
        {
        }
        public ShoppingBasketActionAckMessage(int unk1, byte unk2, ShoppingBasketDto item)
        {
            Unk1 = unk1;
            Unk2 = unk2;
            Item = item;
        }
        public int Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public ShoppingBasketDto Item { get; set; }
    }
    [Packet(1073, PacketType.Game)]
    public class ShoppingBasketListInfoAckMessage
    {
        public ShoppingBasketListInfoAckMessage()
        {
            Items = Array.Empty<ShoppingBasketDto>();
        }
        public ShoppingBasketListInfoAckMessage(ShoppingBasketDto[] items)
        {
            Items = items;
        }
        public ShoppingBasketDto[] Items { get; set; }
    }
    [Packet(1074, PacketType.Game)]
    public class RandomShopUpdateRequestAckMessage
    {
    }
    [Packet(1075, PacketType.Game)]
    public class RandomShopUpdateCheckAckMessage
    {
        public RandomShopUpdateCheckAckMessage()
        {
            Unk = "";
        }
        public RandomShopUpdateCheckAckMessage(string unk)
        {
            Unk = unk;
        }
        public string Unk { get; set; }
    }
    [Packet(1076, PacketType.Game)]
    public class RandomShopUpdateInfoAckMessage
    {
        public RandomShopUpdateInfoAckMessage()
        { }
        public RandomShopUpdateInfoAckMessage(byte unk, byte[] compressedData, int compressedLength, int decompressedLength, string version)
        {
            Unk = unk;
            CompressedData = compressedData;
            CompressedLength = compressedLength;
            DecompressedLength = decompressedLength;
            Version = version;
        }
        public byte Unk { get; set; }
        [Scalar] public byte[] CompressedData { get; set; }
        public int CompressedLength { get; set; }
        public int DecompressedLength { get; set; }
        public string Version { get; set; }
    }
    [Packet(1077, PacketType.Game)]
    public class RandomShopRollingStartAckMessage
    {
        public RandomShopRollingStartAckMessage()
        {
            ItemInfo = Array.Empty<RandomShopItemsDto>();
        }
        public int unk { get; set; }
        public RandomShopItemsDto[] ItemInfo { get; set; }
    }
    [Packet(1078, PacketType.Game)]
    public class RoomInfoRequestAckMessage
    {
        public RoomInfoRequestDto Info { get; set; }
    }
    [Packet(1136, PacketType.Game)]
    public class RoomInfoRequestAck2Message
    {
        public RoomInfoRequestDto Info { get; set; }
    }
    [Packet(1079, PacketType.Game)]
    public class NoteGiftItemAckMessage
    {
        public NoteGiftItemAckMessage()
        { }
        public NoteGiftItemAckMessage(int result)
        {
            Result = result;
        }
        public int Result { get; set; }
    }
    [Packet(1080, PacketType.Game)]
    public class NoteImportuneItemAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1081, PacketType.Game)]
    public class NoteGiftItemGainAckMessage
    {
        public int Unk1 { get; set; }
        public ulong Unk2 { get; set; }
    }
    [Packet(1082, PacketType.Game)]
    public class RoomQuickJoinAckMessage
    {
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
        public RoomQuickJoinAckMessage()
        {
        }
        public RoomQuickJoinAckMessage(byte unk1, int unk2)
        {
            Unk1 = unk1;
            Unk2 = unk2;
        }
    }
    [Packet(1083, PacketType.Game)]
    public class JorbiWebSessionRedirectAckMessage
    {
        public string Unk1 { get; set; }
        public string Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(1084, PacketType.Game)]
    public class CardGambleAckMessage
    {
        public CardGambleAckMessage()
        {
        }
        public CardGambleAckMessage(int result)
            : this()
        {
            Result = result;
        }
        public int Result { get; set; }
        public int ItemId { get; set; }
    }
    [Packet(1137, PacketType.Game)]
    public class AlchemyCombinationAckMessage
    {
        public AlchemyCombinationAckMessage()
        {
            Info = Array.Empty<AlchemyItemDto>();
        }
        public int Unk { get; set; }
        public AlchemyItemDto[] Info { get; set; }
    }
    [Packet(1138, PacketType.Game)]
    public class AlchemyDecompositionAckMessage
    {
        public AlchemyDecompositionAckMessage()
        {
        }
        public AlchemyDecompositionAckMessage(int unk, int unk2, int unk3, int unk4, int unk5, int unk6)
        {
            Unk = unk;
            Unk2 = unk2;
            Unk3 = unk3;
            Unk4 = unk4;
            Unk5 = unk5;
            Unk6 = unk6;
        }
        public int Unk { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
    }
    [Packet(1085, PacketType.Game)]
    public class NoticeItemGainAckMessage
    {
        public int Result { get; set; }
        public string Sender { get; set; }
        public ulong AccountId { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public short Unk6 { get; set; }
        public int Unk7 { get; set; }
    }
    [Packet(1086, PacketType.Game)]
    public class PromotionPunkinNoticeAckMessage
    {
        public int Unk1 { get; set; }
        public byte Unk2 { get; set; }
    }
    [Packet(1087, PacketType.Game)]
    public class PromotionPunkinRankersAckMessage
    {
        public PromotionPunkinRankerDto[] Unk { get; set; }
    }
    [Packet(1088, PacketType.Game)]
    public class RequitalLevelAckMessage
    {
        public RequitalLevelDto[] Unk { get; set; }
    }
    [Packet(1129, PacketType.Game)]
    public class CPromotionNewYearCardUseAckMessage
    {
        public CPromotionNewYearCardUseAckMessage()
        {
        }
        public CPromotionNewYearCardUseAckMessage(int unk)
        {
            Unk = unk;
        }
        public int Unk { get; set; }
    }
    [Packet(1089, PacketType.Game)]
    public class PromotionAttendanceInfoAckMessage
    {
        public int Unk1 { get; set; }
        public int[] Unk2 { get; set; }
    }
    [Packet(1090, PacketType.Game)]
    public class PromotionAttendanceGiftItemAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(1091, PacketType.Game)]
    public class PromotionCoinEventAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(1092, PacketType.Game)]
    public class PromotionCoinEventDropCoinAckMessage
    {
        public uint Ammo { get; set; }
        public uint Unk { get; set; }
        public uint Posions { get; set; }
        public uint Unk2 { get; set; }
    }
    [Packet(1093, PacketType.Game)]
    public class EnchantEnchantItemAckMessage
    {
        public EnchantResult Result { get; set; }
        public ulong ItemId { get; set; }
        public uint Effect { get; set; }
        public EnchantEnchantItemAckMessage()
        {
        }
        public EnchantEnchantItemAckMessage(EnchantResult result)
            : this()
        {
            Result = result;
        }
    }
    [Packet(1193, PacketType.Game)]
    public class MoveEnchantChipAckMessage
    {
        public int Result { get; set; }
    }
    [Packet(1110, PacketType.Game)]
    public class ItemMPRefillAckMessage
    {
        public int Result { get; set; }
    }
    [Packet(1122, PacketType.Game)]
    public class UseInstantItemRemoveEffectAckMessage
    {
        public ulong Unk { get; set; }
        public ulong Unk2 { get; set; }
        public ulong Unk3 { get; set; }
        public uint Unk4 { get; set; }
    }
    [Packet(1094, PacketType.Game)]
    public class EnchantRefreshEnchantGaugeAckMessage
    {
        public EnchantGaugeDto[] Unk { get; set; }
    }
    [Packet(1095, PacketType.Game)]
    public class NoticeEnchantAckMessage
    {
        public NoticeEnchantDto[] Unk { get; set; }
    }
    [Packet(1096, PacketType.Game)]
    public class PromotionCardShuffleAckMessage
    {
        public int Unk1 { get; set; }
        public RequitalLevelDto Unk2 { get; set; }
    }
    [Packet(1097, PacketType.Game)]
    public class ItemClearEsperChipAckMessage
    {
        public ItemClearEsperChipAckMessage()
        {
            Unk = Array.Empty<ClearEsperChipDto>();
        }
        public ClearEsperChipDto[] Unk { get; set; }
    }
    [Packet(1098, PacketType.Game)]
    public class ChallengeMyInfoAckMessage
    {
        public ChallengeMyInfoDto[] Unk { get; set; }
    }
    [Packet(1099, PacketType.Game)]
    public class KRShutDownAckMessage
    {
    }
    [Packet(1100, PacketType.Game)]
    public class RequitalChallengeAckMessage
    {
        public int Unk1 { get; set; }
        public RequitalLevelDto[] Unk2 { get; set; }
    }
    [Packet(1101, PacketType.Game)]
    public class MapOpenInfosMessage
    {
        public MapOpenInfosMessage()
        {
            Unk = Array.Empty<MapOpenInfoDto>();
        }
        public MapOpenInfosMessage(MapOpenInfoDto[] unk)
        {
            Unk = unk;
        }
        public MapOpenInfoDto[] Unk { get; set; }
    }
    [Packet(1102, PacketType.Game)]
    public class PromotionCouponEventAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1103, PacketType.Game)]
    public class TutorialCompletedAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1104, PacketType.Game)]
    public class ExpRefreshInfoAckMessage
    {
        public ExpRefreshInfoAckMessage()
        {
        }
        public ExpRefreshInfoAckMessage(uint totalExp)
        {
            TotalExp = totalExp;
        }
        public uint TotalExp { get; set; }
    }
    [Packet(1105, PacketType.Game)]
    public class PromotionActiveAckMessage
    {
        public PromotionActiveDto[] Unk { get; set; }
    }
    [Packet(1204, PacketType.Game)]
    public class PlayerKickMessageAckMessage
    {
        public int Type { get; set; }
        public string Message { get; set; }
        public PlayerKickMessageAckMessage()
        {
        }
        public PlayerKickMessageAckMessage(int type, string message)
        {
            Type = type;
            Message = message;
        }
    }
    [Packet(1191, PacketType.Game)]
    public class EspherChipLv5Message
    {
        public int Type { get; set; }
        public EspherChipLv5Message()
        {
            Type = -1;
        }
        public EspherChipLv5Message(int type)
        {
            Type = type;
        }
    }
    [Packet(1152, PacketType.Game)]
    public class EsperEnchantAckMessage
    {
        public int Result { get; set; }
        public ulong ItemId { get; set; }
        public uint Effect { get; set; }
        public EsperEnchantAckMessage()
        {
        }
    }
    [SantanaContract]
    public class UseEnchantChipAckMessage
    {
        [SantanaMember(0)]
        public int Result { get; set; }
    }
    [SantanaContract]
    public class ClubNoticePointRefreshAckMessage
    {
        public ClubNoticePointRefreshAckMessage()
        {
            Unk6 = 3;
            Unk8 = 1;
            Unk9 = 2;
        }
        [SantanaMember(0)] public uint Unk1 { get; set; }
        [SantanaMember(1)] public uint Unk2 { get; set; }
        [SantanaMember(2)] public uint Unk3 { get; set; }
        [SantanaMember(3)] public uint Unk4 { get; set; }
        [SantanaMember(4)] public uint Unk5 { get; set; }
        [SantanaMember(5)] public uint Unk6 { get; set; }
        [SantanaMember(6)] public uint Unk7 { get; set; }
        [SantanaMember(7)] public uint Unk8 { get; set; }
        [SantanaMember(8)] public uint Unk9 { get; set; }
    }
    [SantanaContract]
    public class EsperEnchantPercentUpAckMessage
    {
        [SantanaMember(0)]
        public int Unk { get; set; }
        public EsperEnchantPercentUpAckMessage()
        {
        }
        public EsperEnchantPercentUpAckMessage(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(1143, PacketType.Game)]
    public class DailyMission_NoticeMessage
    {
        public DailyMission_NoticeMessage()
        { }
        public int Unk { get; set; }
        public int GameMode { get; set; }
        public int Map { get; set; }
        public int MaxProgress { get; set; }
        public int Progress { get; set; }
        public int Unk5 { get; set; }
        public int[] Unk6 { get; set; }
    }
    [Packet(1148, PacketType.Game)]
    public class Btc_Sync_NoticeMessage
    {
        public int Unk { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
    }
    [Packet(1147, PacketType.Game)]
    public class Btc_Clear_AckMessage
    {
        public int Unk { get; set; }
        public BTCGiveItemResultDto[] Unk2 { get; set; }
    }
    [Packet(1145, PacketType.Game)]
    public class AchieveMissionAckMessage
    {
        public int[] Unk { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(1146, PacketType.Game)]
    public class AchieveMissionRewardAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1144, PacketType.Game)]
    public class DailyMissionRewardAckMessage
    {
        public int unk1 { get; set; }
    }
    [Packet(1111, PacketType.Game)]
    public class CollectBookInvenEffectInfoAckMessage
    {
        public int Unk { get; set; }
        public byte active { get; set; }
        public short Unk3 { get; set; }
        public int Unk4 { get; set; }
        public uint nametagid { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public string days { get; set; }
        public string nametag { get; set; }
        public string zero { get; set; }
        public string zero2 { get; set; }
        public string zero3 { get; set; }
    }
    [Packet(1201, PacketType.Game)]
    public class BattleyeS2CDataMessage
    {
        public BattleyeS2CDataMessage(byte[] data)
        {
            if (data.Length < 1)
                return;
            DataSize = data.Length;
            Data = data;
        }
        public BattleyeS2CDataMessage()
        {
        }
        public int DataSize { get; set; }
        [Scalar] public byte[] Data { get; set; }
    }
    [Dto]
    public class CollectBookEffectItem
    {
        public byte Unk1 { get; set; }
        public int Unk2 { get; set; }
        public short Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public string Unk7 { get; set; }
        public string Unk8 { get; set; }
        public string Unk9 { get; set; }
        public string Unk10 { get; set; }
        public string Unk11 { get; set; }
    }
    [Packet(1106, PacketType.Game)]
    public class CollectBook_UpdateRequest_Ack
    {
    }
    [Packet(1107, PacketType.Game)]
    public class CollectBook_UpdateCheck_Ack
    {
        public string Data { get; set; }
    }
    [Packet(1108, PacketType.Game)]
    public class CollectBook_UpdateInfo_Ack
    {
        public CollectBook_UpdateInfo_Ack()
        {
            Unk1 = Array.Empty<byte>();
            Unk4 = string.Empty;
        }
        [Scalar] public byte[] Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public string Unk4 { get; set; }
    }
    [Packet(1109, PacketType.Game)]
    public class CollectBook_InventoryInfo_Ack
    {
        public CollectBook_ItemRegist_Ack[] Items { get; set; }
    }
    [Packet(1111, PacketType.Game)]
    public class CollectBook_InvenEffectInfo_Ack
    {
        public CollectBookEffectItem[] Items { get; set; }
    }
    [Packet(1112, PacketType.Game)]
    public class CollectBook_ItemRegist_Ack
    {
        public ulong Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
    }
    [Packet(1113, PacketType.Game)]
    public class CollectBook_EffectRegist_Ack
    {
        public int Value { get; set; }
        public CollectBookEffectItem[] Items { get; set; }
    }
    [Packet(1114, PacketType.Game)]
    public class CollectBook_ExpireBookReward_Ack
    {
        public int Unk { get; set; }
    }
    [Packet(1115, PacketType.Game)]
    public class CollectBook_ResuseBookReward_Ack
    {
        public uint Value { get; set; }
    }
    [Packet(1116, PacketType.Game)]
    public class CollectBook_BookUseReward_Ack
    {
        public int Value { get; set; }
        public BookUseRewardData Data { get; set; }
    }
    [Dto]
    public class BookUseRewardData
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public string Unk5 { get; set; }
        public string Unk6 { get; set; }
        public string Unk7 { get; set; }
    }
    [Packet(1117, PacketType.Game)]
    public class CollectBook_BookUnRegist_Ack
    {
        public int Value { get; set; }
    }
    [Packet(1166, PacketType.Game)]
    public class ClubNotice_Point_Refresh_Ack
    {
        public int Unk1 { get; set; }
        public ulong Unk2 { get; set; }
        public ulong Unk3 { get; set; }
        public int[] Unk4 { get; set; } = Array.Empty<int>();
    }
    [Packet(1167, PacketType.Game)]
    public class ClubNotice_Record_Refresh_Ack
    {
        public int Unk1 { get; set; }
        public ClubNoticeRecordDto[] Info { get; set; } = Array.Empty<ClubNoticeRecordDto>();
    }
    [Dto]
    public class ClubNoticeRecordDto
    {
        public string Unk1 { get; set; }
        public string Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public string[] Unk7 { get; set; } = Array.Empty<string>();
        public string[] Unk8 { get; set; } = Array.Empty<string>();
    }
    [Packet(1170, PacketType.Game)]
    public class NukingUserAckMessage
    {
        public short unk { get; set; }
    }
    [Packet(1174, PacketType.Game)]
    public class ClubOtherClubInfoAckMessage
    {
        public ClubOtherClubInfoAckMessage()
        {
            Data = new ClubOtherClubInfo();
        }
        public ClubOtherClubInfoAckMessage(int result, ClubOtherClubInfo data)
        {
            Result = result;
            Data = data;
        }
        public int Result { get; set; }
        public ClubOtherClubInfo Data { get; set; }
    }
    [Dto]
    public class ClubOtherClubInfo
    {
        public int Unk1 { get; set; }
        public string Unk2 { get; set; }
        public string Unk3 { get; set; }
        public string Unk4 { get; set; }
        public ulong Unk5 { get; set; }
        public int Unk6 { get; set; }
        public string Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
        public string Unk10 { get; set; }
        public string Unk11 { get; set; }
        public string Unk12 { get; set; }
        public string Unk13 { get; set; }
        public int Unk14 { get; set; }
        public int Unk15 { get; set; }
        public int Unk16 { get; set; }
        public int Unk17 { get; set; }
        public int Unk18 { get; set; }
        public int Unk19 { get; set; }
        public int Unk20 { get; set; }
        public int Unk21 { get; set; }
        public int Unk22 { get; set; }
        public short Unk23 { get; set; }
        public int Unk24 { get; set; }
    }
    [Packet(1176, PacketType.Game)]
    public class ClubEndGameLeaderPointAckMessage
    {
        public ulong Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(1177, PacketType.Game)]
    public class ClubEndGameClubPointAckMessage
    {
        public ulong Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(1178, PacketType.Game)]
    public class ClubFASystemChangeStateAckMessage
    {
        public int State { get; set; }
    }
    [Packet(1179, PacketType.Game)]
    public class ClubFASystemFAListAckMessage
    {
        public int Unk { get; set; }
        public List<ClubFAItem> Items { get; set; }
    }
    [Dto]
    public class ClubFAItem
    {
        public string Name { get; set; }
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public byte Unk3 { get; set; }
    }
    [Packet(1180, PacketType.Game)]
    public class ClubFASystemFARegisterAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(1181, PacketType.Game)]
    public class ClubFASystemFARegisterConfirmAckMessage
    {
        public byte Result { get; set; }
    }
    [Packet(1182, PacketType.Game)]
    public class ClubFASystemFARecordAckMessage
    {
        public int Unk { get; set; }
        public List<ClubFARecordItem> Items { get; set; }
    }
    [Packet(1183, PacketType.Game)]
    public class ClubUnionSystemUserInfoAckMessage
    {
        public ClubUnionSystemUserInfoAckMessage()
        {
            Data = new ClubUnionUserInfo();
        }
        public ClubUnionUserInfo Data { get; set; }
    }
    [Dto]
    public class ClubUnionUserInfo
    {
        public ulong Unk1 { get; set; }
        public int Unk2 { get; set; }
        public byte Unk3 { get; set; }
    }
    [Packet(1184, PacketType.Game)]
    public class ClubFASystemFAScoutAckMessage
    {
        public ulong A { get; set; }
        public int B { get; set; }
    }
    [Packet(1185, PacketType.Game)]
    public class ClubFASystemFAScoutListAckMessage
    {
        public int Count { get; set; }
        public List<ClubFAScoutItem> Items { get; set; }
    }
    public class ClubFAScoutItem
    {
        public int A;
        public string B;
        public ulong C;
        public int D;
        public int E;
    }
    [Packet(1186, PacketType.Game)]
    public class ClubFASystemFAScoutAcceptAckMessage
    {
        public int A { get; set; }
        public int B { get; set; }
        public ulong C { get; set; }
        public ulong D { get; set; }
        public string E { get; set; }
        public byte F { get; set; }
    }
    [Packet(1187, PacketType.Game)]
    public class UnionEndGamePointAckMessage
    {
        public ulong Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(1188, PacketType.Game)]
    public class UnionMainUiAckMessage
    {
        public UnionMainUiAckMessage()
        {
            Data = new UnionMainUiDto();
        }
        public UnionMainUiDto Data { get; set; }
        public int Unk1 { get; set; }
    }
    [Dto]
    public class UnionMainUiDto
    {
        public UnionMainUiDto()
        {
            Unk1 = Array.Empty<UnionMainUiListEntryDto>();
            Unk2 = Array.Empty<UnionMainUiListEntryDto>();
            Unk3 = Array.Empty<UnionMainUiListEntryDto>();
            Unk4 = Array.Empty<UnionMainUiListEntryDto>();
            Unk5 = Array.Empty<UnionMainUiListEntryDto>();
            Unk6 = Array.Empty<UnionMainUiListEntryDto>();
            Unk7 = Array.Empty<UnionMainUiListEntryDto>();
            Unk8 = new UnionMainUiSummaryDto();
        }
        public UnionMainUiListEntryDto[] Unk1 { get; set; }
        public UnionMainUiListEntryDto[] Unk2 { get; set; }
        public UnionMainUiListEntryDto[] Unk3 { get; set; }
        public UnionMainUiListEntryDto[] Unk4 { get; set; }
        public UnionMainUiListEntryDto[] Unk5 { get; set; }
        public UnionMainUiListEntryDto[] Unk6 { get; set; }
        public UnionMainUiListEntryDto[] Unk7 { get; set; }
        public UnionMainUiSummaryDto Unk8 { get; set; }
    }
    [Dto]
    public class UnionMainUiListEntryDto
    {
        public UnionMainUiListEntryDto()
        {
            Unk1 = "";
        }
        public string Unk1 { get; set; }
        public short Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Dto]
    public class UnionMainUiSummaryDto
    {
        public UnionMainUiSummaryDto()
        {
            Unk8 = "";
            Unk9 = "";
            Unk13 = new UnionMainUiStatRowDto();
            Unk14 = new UnionMainUiStatRowDto();
            Unk15 = new UnionMainUiStatRowDto();
            Unk16 = new UnionMainUiStatRowDto();
            Unk17 = new UnionMainUiStatRowDto();
            Unk18 = new UnionMainUiStatRowDto();
            Unk19 = new UnionMainUiStatRowDto();
            Unk20 = new UnionMainUiStatRowDto();
            Unk21 = new UnionMainUiStatRowDto();
            Unk22 = new UnionMainUiStatRowDto();
            Unk23 = new UnionMainUiStatRowDto();
            Unk24 = new UnionMainUiStatRowDto();
            Unk25 = new UnionMainUiStatRowDto();
            Unk26 = new UnionMainUiStatRowDto();
        }
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
        public byte Unk4 { get; set; }
        public byte Unk5 { get; set; }
        public byte Unk6 { get; set; }
        public byte Unk7 { get; set; }
        public string Unk8 { get; set; }
        public string Unk9 { get; set; }
        public int Unk10 { get; set; }
        public int Unk11 { get; set; }
        public int Unk12 { get; set; }
        public UnionMainUiStatRowDto Unk13 { get; set; }
        public UnionMainUiStatRowDto Unk14 { get; set; }
        public UnionMainUiStatRowDto Unk15 { get; set; }
        public UnionMainUiStatRowDto Unk16 { get; set; }
        public UnionMainUiStatRowDto Unk17 { get; set; }
        public UnionMainUiStatRowDto Unk18 { get; set; }
        public UnionMainUiStatRowDto Unk19 { get; set; }
        public UnionMainUiStatRowDto Unk20 { get; set; }
        public UnionMainUiStatRowDto Unk21 { get; set; }
        public UnionMainUiStatRowDto Unk22 { get; set; }
        public UnionMainUiStatRowDto Unk23 { get; set; }
        public UnionMainUiStatRowDto Unk24 { get; set; }
        public UnionMainUiStatRowDto Unk25 { get; set; }
        public UnionMainUiStatRowDto Unk26 { get; set; }
    }
    [Dto]
    public class UnionMainUiStatRowDto
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
        public int Unk5 { get; set; }
        public int Unk6 { get; set; }
        public int Unk7 { get; set; }
        public int Unk8 { get; set; }
        public int Unk9 { get; set; }
    }
    [Packet(1189, PacketType.Game)]
    public class UnionSeasonEndAckMessage
    {
        public byte Unk1 { get; set; }
        public byte Unk2 { get; set; }
        public byte Unk3 { get; set; }
    }
    [Packet(1190, PacketType.Game)]
    public class UnionSearchRoomAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Dto]
    public class ClubFARecordItem
    {
        public int V0 { get; set; }
        public int V1 { get; set; }
        public int V2 { get; set; }
        public int V3 { get; set; }
        public int V4 { get; set; }
        public int V5 { get; set; }
        public int V6 { get; set; }
        public int V7 { get; set; }
        public int V8 { get; set; }
        public int V9 { get; set; }
        public int V10 { get; set; }
        public int V11 { get; set; }
        public int V12 { get; set; }
        public int V13 { get; set; }
        public int V14 { get; set; }
        public int V15 { get; set; }
        public int V16 { get; set; }
        public int V17 { get; set; }
    }
}
[Packet(3101, PacketType.GameRule)]
public class ArenaSpecialPointAckMessage
{
    public int AssistPoint { get; set; }
    public ulong AccountId { get; set; }
    public uint Point { get; set; }
    public ArenaSpecialPointAckMessage()
    {
    }
    public ArenaSpecialPointAckMessage(int assistPoint, ulong userId, uint point)
    {
        AssistPoint = assistPoint;
        AccountId = userId;
        Point = point;
    }
}
[Packet(3102, PacketType.GameRule)]
public class ScoreArenaBattlePlayerLeaveMessage
{
    public byte WinPoint { get; set; }
    public ulong AccountId { get; set; }
    public ulong Unk1 { get; set; }
    public ScoreArenaBattlePlayerLeaveMessage()
    {
    }
    public ScoreArenaBattlePlayerLeaveMessage(byte winPoint, ulong accountId, ulong unk1)
    {
        WinPoint = winPoint;
        AccountId = accountId;
        Unk1 = unk1;
    }
}
[Packet(3103, PacketType.GameRule)]
public class ArenaLeaderShowdwonMessage
{
}
[Packet(3104, PacketType.GameRule)]
public class ArenaDrawHealthPointReqMessage
{
}
[Packet(3105, PacketType.GameRule)]
public class ArenaHalfTimeStatusMessage
{
    public GameTimeState TimeState { get; set; }
    public Team TeamId { get; set; }
    public TimeSpan RoundTime { get; set; }
    public ArenaHalfTimeStatusMessage()
    {
    }
    public ArenaHalfTimeStatusMessage(GameTimeState timeState, Team teamId, TimeSpan roundTime)
    {
        TimeState = timeState;
        TeamId = teamId;
        RoundTime = roundTime;
    }
}
[Packet(3106, PacketType.GameRule)]
public class ArenaNotIntrudePlayerMessage
{
}

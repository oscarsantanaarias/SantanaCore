using System;
using SantanaLib.Serialization;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.Club
{
    [Packet(4001, PacketType.Club)]
    public class ClubCreateReqMessage
    {
#if LATESTS4
        public string Name { get; set; }
        public string Unk2 { get; set; }
        public string Unk3 { get; set; }
#else
        public string Mark { get; set; }
        public string Name { get; set; }
        public string Introduction { get; set; }
        public int ActivityArea { get; set; }
        public int ActivityPurpose { get; set; }
        public string Question1 { get; set; }
        public string Question2 { get; set; }
        public string Question3 { get; set; }
        public string Question4 { get; set; }
        public string Question5 { get; set; }
#endif
    }
    [Packet(4002, PacketType.Club)]
    public class ClubCloseReqMessage
    {
        public int ClanId { get; set; }
    }
    [Packet(4036, PacketType.Club)]
    public class ClubCloseReq2Message
    {
        public int ClanId { get; set; }
    }
    [Packet(4004, PacketType.Club)]
    public class ClubUnjoinReqMessage
    {
        public int ClanId { get; set; }
    }
    [Packet(4005, PacketType.Club)]
    public class ClubNameCheckReqMessage
    {
        public string Name { get; set; }
    }
    [Packet(4006, PacketType.Club)]
    public class ClubRestoreReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4003, PacketType.Club)]
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
    [Packet(4007, PacketType.Club)]
    public class ClubAdminInviteReqMessage
    {
        public ulong AccountId { get; set; }
    }
    [Packet(4008, PacketType.Club)]
    public class ClubAdminJoinCommandReqMessage
    {
        public uint Command { get; set; }
        public ulong[] AccountId { get; set; }
    }
    [Packet(4009, PacketType.Club)]
    public class ClubAdminGradeChangeReqMessage
    {
        public ClubAdminGradeChangeReqMessage()
        {
            Changes = Array.Empty<ClubAdminGradeChangeDto>();
        }
        public ClubAdminGradeChangeDto[] Changes { get; set; }
    }
    [Dto]
    public class ClubAdminGradeChangeDto
    {
        public ulong AccountId { get; set; }
        public int Rank { get; set; }
    }
    [Packet(4010, PacketType.Club)]
    public class ClubAdminNoticeChangeReqMessage
    {
        public string Notice { get; set; }
    }
    [Packet(4011, PacketType.Club)]
    public class ClubAdminInfoModifyReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public string Unk3 { get; set; }
    }
    [Packet(4012, PacketType.Club)]
    public class ClubAdminSubMasterReqMessage
    {
        public ulong Target { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(4013, PacketType.Club)]
    public class ClubAdminSubMasterCancelReqMessage
    {
        public ulong Target { get; set; }
    }
    [Packet(4014, PacketType.Club)]
    public class ClubAdminMasterChangeReqMessage
    {
        public ulong Target { get; set; }
    }
    [Packet(4015, PacketType.Club)]
    public class ClubAdminJoinConditionModifyReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public string Unk3 { get; set; }
        public string Unk4 { get; set; }
        public string Unk5 { get; set; }
        public string Unk6 { get; set; }
        public string Unk7 { get; set; }
    }
    [Packet(4016, PacketType.Club)]
    public class ClubAdminBoardModifyReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
    }
    [Packet(4017, PacketType.Club)]
    public class ClubSearchReqMessage
    {
        public ClubSearchType SearchType { get; set; }
        public string Query { get; set; }
        public int Page { get; set; }
        public ClubSearchSort SortBy { get; set; }
        public ClubSearchSortType SortType { get; set; }
    }
    [Packet(4041, PacketType.Club)]
    public class ClubSearchReq2Message
    {
        public int ClubId { get; set; }
        public string ClubName { get; set; }
        public int PageNo { get; set; }
        public int Unk4 { get; set; }
        public byte Unk5 { get; set; }
    }
    [Packet(4018, PacketType.Club)]
    public class ClubClubInfoReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4040, PacketType.Club)]
    public class ClubClubInfoReq2Message
    {
        public int Unk { get; set; }
    }
    [Packet(4019, PacketType.Club)]
    public class ClubJoinWaiterInfoReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4020, PacketType.Club)]
    public class ClubNewJoinMemberInfoReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4021, PacketType.Club)]
    public class ClubJoinConditionInfoReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4022, PacketType.Club)]
    public class ClubUnjoinerListReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4023, PacketType.Club)]
    public class ClubUnjoinSettingMemberListReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(4024, PacketType.Club)]
    public class ClubGradeCountReqMessage
    {
        public uint ClubId { get; set; }
    }
    [Packet(4025, PacketType.Club)]
    public class ClubStuffListReqMessage
    {
        public uint ClanId { get; set; }
    }
    [Packet(4044, PacketType.Club)]
    public class ClubStuffListReq2Message
    {
        public int ClanId { get; set; }
    }
    [Packet(4026, PacketType.Club)]
    public class ClubNewsInfoReqMessage
    {
        public uint ClanId { get; set; }
    }
    [Packet(4027, PacketType.Club)]
    public class ClubBoardWriteReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public string Unk4 { get; set; }
    }
    [Packet(4028, PacketType.Club)]
    public class ClubBoardReadReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(4029, PacketType.Club)]
    public class ClubBoardModifyReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public string Unk4 { get; set; }
    }
    [Packet(4030, PacketType.Club)]
    public class ClubBoardDeleteReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(4031, PacketType.Club)]
    public class ClubBoardDeleteAllReqMessage
    {
        public int Unk { get; set; }
    }
    [Packet(4032, PacketType.Club)]
    public class ClubBoardSearchNickReqMessage
    {
        public int Unk1 { get; set; }
        public string Unk2 { get; set; }
        public int Unk3 { get; set; }
    }
    [Packet(4033, PacketType.Club)]
    public class ClubBoardReadOtherClubReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(4034, PacketType.Club)]
    public class ClubBoardReadMineReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(4035, PacketType.Club)]
    public class ClubCreateReq2Message
    {
        public string Name { get; set; }
        public string Unk2 { get; set; }
        public string Unk3 { get; set; }
    }
    [Packet(4045, PacketType.Club)]
    public class ClubRankListReqMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
    }
    [Packet(4038, PacketType.Club)]
    public class ClubUnjoinReq2Message
    {
        public int ClanId { get; set; }
    }
    [Packet(4037, PacketType.Club)]
    public class ClubJoinReq2Message
    {
        public uint ClanId { get; set; }
#if !LATESTS4
        public string ClanName { get; set; }
#endif
    }
}

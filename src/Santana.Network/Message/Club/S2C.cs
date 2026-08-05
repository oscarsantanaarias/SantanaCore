using System;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Club;
using ProudNetSrc.Serialization;
namespace Santana.Network.Message.Club
{
    [Packet(5001, PacketType.Club)]
    public class ClubCreateAckMessage
    {
        public int Unk { get; set; }
        public ClubCreateAckMessage()
        {
        }
        public ClubCreateAckMessage(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(5034, PacketType.Club)]
    public class ClubCreateAck2Message
    {
        public int Unk { get; set; }
#if !LATESTS4
        public int ClanId { get; set; }
#endif
        public ClubCreateAck2Message()
        {
        }
        public ClubCreateAck2Message(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(5002, PacketType.Club)]
    public class ClubCloseAckMessage
    {
        public int Result { get; set; }
    }
    [Packet(5035, PacketType.Club)]
    public class ClubCloseAck2Message
    {
        public int Result { get; set; }
        public ClubCloseAck2Message()
        {
        }
        public ClubCloseAck2Message(int result)
        {
            Result = result;
        }
    }
    [Packet(5003, PacketType.Club)]
    public class ClubJoinAckMessage
    {
        public ClubJoinResult Unk { get; set; }
    }
    [Packet(5036, PacketType.Club)]
    public class ClubJoinAck2Message
    {
        public int Unk { get; set; }
        public ClubJoinAck2Message()
        {
        }
        public ClubJoinAck2Message(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(5004, PacketType.Club)]
    public class ClubUnjoinAckMessage
    {
        public int Result { get; set; }
        public ClubUnjoinAckMessage()
        {
        }
        public ClubUnjoinAckMessage(int result)
        {
            Result = result;
        }
    }
    [Packet(5005, PacketType.Club)]
    public class ClubNameCheckAckMessage
    {
        public int Unk { get; set; }
        public ClubNameCheckAckMessage()
        {
            Unk = 0;
        }
        public ClubNameCheckAckMessage(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(5006, PacketType.Club)]
    public class ClubRestoreAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5007, PacketType.Club)]
    public class ClubAdminInviteAckMessage
    {
        public int Unk { get; set; }
        public ClubAdminInviteAckMessage()
        {
        }
        public ClubAdminInviteAckMessage(int unk)
        {
            Unk = unk;
        }
    }
    [Packet(5008, PacketType.Club)]
    public class ClubAdminJoinCommandAckMessage
    {
        public uint Unk { get; set; }
#if LATESTS4
        public ulong Unk2 { get; set; }
#else
        public ulong[] Unk2 { get; set; }
#endif
        public ClubAdminJoinCommandAckMessage()
        {
#if !LATESTS4
            Unk2 = Array.Empty<ulong>();
#endif
        }
        public ClubAdminJoinCommandAckMessage(uint unk, ulong unk2)
        {
            Unk = unk;
#if LATESTS4
            Unk2 = unk2;
#else
            Unk2 = new[] { unk2 };
#endif
        }
    }
    [Packet(5009, PacketType.Club)]
    public class ClubAdminGradeChangeAckMessage
    {
        public int Unk { get; set; }
        public ulong[] Target { get; set; }
        public ClubAdminGradeChangeAckMessage()
        {
        }
    }
    [Packet(5010, PacketType.Club)]
    public class ClubAdminNoticeChangeAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5011, PacketType.Club)]
    public class ClubAdminInfoModifyAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5012, PacketType.Club)]
    public class ClubAdminSubMasterAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5013, PacketType.Club)]
    public class ClubAdminSubMasterCancelAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5014, PacketType.Club)]
    public class ClubAdminMasterChangeAckMessage
    {
        public ClanMasterChangeMessage Message { get; set; }
        public ClubAdminMasterChangeAckMessage()
        {
        }
        public ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage message)
        {
            Message = message;
        }
    }
    [Packet(5015, PacketType.Club)]
    public class ClubAdminJoinConditionModifyAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5016, PacketType.Club)]
    public class ClubAdminBoardModifyAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5017, PacketType.Club)]
    public class ClubSearchAckMessage
    {
        public int ResultCount { get; set; }
        public ClubSearchResultDto[] Clubs { get; set; }
        public ClubSearchAckMessage()
        {
            Clubs = Array.Empty<ClubSearchResultDto>();
        }
        public ClubSearchAckMessage(ClubSearchResultDto[] clubs)
        {
            ResultCount = clubs.Length;
            Clubs = clubs;
        }
    }
    [Packet(5040, PacketType.Club)]
    public class ClubSearchAck2Message
    {
        public int Unk1 { get; set; }
        public ClubRankInfoDto[] Clubs { get; set; }
        public ClubSearchAck2Message()
        {
            Clubs = Array.Empty<ClubRankInfoDto>();
        }
        public ClubSearchAck2Message(int unk1, ClubRankInfoDto[] clubs)
        {
            Unk1 = unk1;
            Clubs = clubs;
        }
    }
    [Packet(5018, PacketType.Club)]
    public class ClubClubInfoAckMessage
    {
        public ClubInfoDto Info { get; set; }
        public ClubClubInfoAckMessage()
        {
            Info = new ClubInfoDto();
        }
        public ClubClubInfoAckMessage(ClubInfoDto info)
        {
            Info = info;
        }
    }
    [Packet(5039, PacketType.Club)]
    public class ClubClubInfoAck2Message
    {
#if LATESTS4
        public ClubInfoDto2 Info { get; set; }
        public ClubClubInfoAck2Message()
        {
            Info = new ClubInfoDto2();
        }
        public ClubClubInfoAck2Message(ClubInfoDto2 info)
        {
            Info = info;
        }
#else
        public int Unk1 { get; set; }
        public ClubRankInfoDto Info { get; set; }
        public ClubClubInfoAck2Message()
        {
            Info = new ClubRankInfoDto();
        }
        public ClubClubInfoAck2Message(ClubInfoDto2 info)
        {
            Info = new ClubRankInfoDto
            {
                ClanId = info.Id,
                Name = info.Name,
                ClanIcon = info.Type,
                Unk2 = string.Empty,
                MasterName = info.MasterName,
                Unk6 = info.MemberCount,
                Unk8 = string.Empty,
                CreationDate = info.CreationDate,
                Unk9 = info.Motto,
                Unk10 = string.Empty
            };
        }
#endif
    }
    [Packet(5019, PacketType.Club)]
    public class ClubJoinWaiterInfoAckMessage
    {
        public JoinWaiterInfoDto[] Member { get; set; }
        public ClubJoinWaiterInfoAckMessage()
        {
            Member = Array.Empty<JoinWaiterInfoDto>();
        }
        public ClubJoinWaiterInfoAckMessage(JoinWaiterInfoDto[] member)
        {
            Member = member ?? Array.Empty<JoinWaiterInfoDto>();
        }
    }
    [Packet(5020, PacketType.Club)]
    public class ClubNewJoinMemberInfoAckMessage
    {
        public ClubMemberInfoDto[] Unk { get; set; }
        public ClubNewJoinMemberInfoAckMessage()
        {
            Unk = Array.Empty<ClubMemberInfoDto>();
        }
        public ClubNewJoinMemberInfoAckMessage(ClubMemberInfoDto[] unk)
        {
            Unk = unk;
        }
    }
    [Packet(5021, PacketType.Club)]
    public class ClubJoinConditionInfoAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public string Unk3 { get; set; }
        public string Unk4 { get; set; }
        public string Unk5 { get; set; }
        public string Unk6 { get; set; }
        public string Unk7 { get; set; }
    }
    [Packet(5022, PacketType.Club)]
    public class ClubUnjoinerListAckMessage
    {
        public UnjoinerDto[] Unk { get; set; }
    }
    [Packet(5023, PacketType.Club)]
    public class ClubUnjoinSettingMemberListAckMessage
    {
        public UnjoinSettingMemberDto[] Unk { get; set; }
    }
    [Packet(5024, PacketType.Club)]
    public class ClubGradeCountAckMessage
    {
        public int Unk1 { get; set; }
        public int Unk2 { get; set; }
        public int Unk3 { get; set; }
        public int Unk4 { get; set; }
    }
    [Packet(5025, PacketType.Club)]
    public class ClubStuffListAckMessage
    {
        public ClubMemberDto[] Members { get; set; }
        public ClubStuffListAckMessage()
        {
            Members = Array.Empty<ClubMemberDto>();
        }
        public ClubStuffListAckMessage(ClubMemberDto[] members)
        {
            Members = members;
        }
    }
    [Packet(5043, PacketType.Club)]
    public class ClubStuffListAck2Message
    {
        public ClubMemberDto2[] Members { get; set; }
        public ClubStuffListAck2Message()
        {
            Members = Array.Empty<ClubMemberDto2>();
        }
        public ClubStuffListAck2Message(ClubMemberDto2[] members)
        {
            Members = members;
        }
    }
    [Packet(5026, PacketType.Club)]
    public class ClubNewsInfoAckMessage
    {
#if LATESTS4
        public int Unk1 { get; set; }
        public string Unk2 { get; set; }
        public string Unk3 { get; set; }
#else
        public ClubNewsDto[] News { get; set; }
        public ClubNewsInfoAckMessage()
        {
            News = Array.Empty<ClubNewsDto>();
        }
#endif
    }
    [Packet(5027, PacketType.Club)]
    public class ClubMyInfoAckMessage
    {
        public ClubMyInfoDto Unk { get; set; }
        public ClubMyInfoAckMessage()
        {
        }
        public ClubMyInfoAckMessage(ClubMyInfoDto unk)
        {
            Unk = unk;
        }
    }
    [Packet(5028, PacketType.Club)]
    public class ClubBoardWriteAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5029, PacketType.Club)]
    public class ClubBoardReadAckMessage
    {
#if !LATESTS4
        public int Unk1 { get; set; }
#endif
        public BoardMessageDto[] Unk { get; set; }
    }
    [Packet(5030, PacketType.Club)]
    public class ClubBoardModifyAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5031, PacketType.Club)]
    public class ClubBoardDeleteAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5032, PacketType.Club)]
    public class ClubBoardDeleteAllAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5033, PacketType.Club)]
    public class ClubBoardReadFailedAckMessage
    {
        public int Unk { get; set; }
    }
    [Packet(5044, PacketType.Club)]
    public class ClubRankListAckMessage
    {
        public int TotalClans { get; set; }
        public ClubRankInfoDto[] Clans { get; set; }
        public ClubRankListAckMessage()
        {
            Clans = Array.Empty<ClubRankInfoDto>();
        }
        public ClubRankListAckMessage(int totalClans, ClubRankInfoDto[] clans)
        {
            TotalClans = totalClans;
            Clans = clans;
        }
    }
    [Packet(5037, PacketType.Club)]
    public class ClubUnjoinAck2Message
    {
        public int Result { get; set; }
        public ClubUnjoinAck2Message()
        {
        }
        public ClubUnjoinAck2Message(int result)
        {
            Result = result;
        }
    }
}


using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Chat
{
  [Dto]
  public class ClubMemberDto
  {
    public ClubMemberDto(ulong accountId, string nickname, int serverid, int channelid, int roomid)
    {
      AccountId = accountId;
      Nickname = nickname;
    }

    public ClubMemberDto()
    {
      Nickname = "";
      SignInDate = "";
      LastLogin = "";

    }

     public ulong AccountId { get; set; }

    public string Nickname { get; set; }

     public int Unk1 { get; set; }

     public ClubRank ClanRank { get; set; }

     public int Rank { get; set; }

     public int Unk5 { get; set; }

    public string SignInDate { get; set; }

    public string LastLogin { get; set; }

     public int Status { get; set; }
  }

  [Dto]
  public class ClubMemberDto2
  {
    public ClubMemberDto2(ulong accountId, string nickname, int serverid, int channelid, int roomid)
    {
      AccountId = accountId;
      Nickname = nickname;
      ServerId = serverid;
      ChannelId = channelid;
      RoomId = roomid;
    }

    public ClubMemberDto2()
    {
      Nickname = "";
      JoinDate = "";
      LastLogin = "";
      ServerId = -1;
      ChannelId = -1;
      RoomId = -1;

    }

     public ulong AccountId { get; set; }

    public string Nickname { get; set; }

     public int Unk1 { get; set; }

     public int Unk2 { get; set; }

     public ClubRank ClanRank { get; set; }

     public int Unk4 { get; set; }

     public int Unk5 { get; set; }

    public string JoinDate { get; set; }

     public int Unk7 { get; set; }

    public string LastLogin { get; set; }

     public int ServerId { get; set; }

     public int ChannelId { get; set; }

     public int RoomId { get; set; }

     public int Unk11 { get; set; }

     public int Unk12 { get; set; }
  }
}

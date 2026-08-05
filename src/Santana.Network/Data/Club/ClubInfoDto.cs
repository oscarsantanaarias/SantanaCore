
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class ClubInfoDto
  {
    public ClubInfoDto()
    {
      Type = "";
      Name = "";
      MasterName = "";
      CreationDate = "";
#if LATESTS4
      Unk7 = 1;
#else
      BoardAccess = 1;
#endif
      Motto = " ";
      Announce = "";
    }

     public uint Id { get; set; }

    public string Type { get; set; }

    public string Name { get; set; }

     public int MemberCount { get; set; }

    public string MasterName { get; set; }

    public string CreationDate { get; set; }

#if LATESTS4
     public int Unk1 { get; set; }

     public int Unk2 { get; set; }

     public int Unk3 { get; set; }

     public int Unk4 { get; set; }

     public int Unk5 { get; set; }

     public int Unk6 { get; set; }
#else
     public int Area { get; set; }

     public int Activity { get; set; }

     public int Wins { get; set; }

     public int Losses { get; set; }

     public int ClanClass { get; set; }

     public int ClanRank { get; set; }
#endif

    public string Motto { get; set; }

    public string Announce { get; set; }

#if LATESTS4
     public int Unk7 { get; set; }

     public int Unk8 { get; set; }

     public int Unk9 { get; set; }

     public int Unk10 { get; set; }
#else
     public int BoardAccess { get; set; }

     public int BoardAccessBadManner { get; set; }

     public int BoardPost { get; set; }

     public int BoardPostBadManner { get; set; }
#endif
  }
}

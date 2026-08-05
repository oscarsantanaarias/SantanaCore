
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class ClubMyInfoDto
  {
    public ClubMyInfoDto()
    {
      Type = "";
      Id = 0;
      State = 0;
#if LATESTS4
      Unk5 = -1;
#endif
    }

     public uint Id { get; set; }

    public string Type { get; set; }

    public string Name { get; set; }

     public ClubState State { get; set; }

     public int Unk1 { get; set; }

     public ClubRank Rank { get; set; }

     public int Unk2 { get; set; }

     public int Unk3 { get; set; }

#if LATESTS4
     public int Unk4 { get; set; }

     public long Unk5 { get; set; }

     public int Unk6 { get; set; }
#else
     public int LeaguePoints { get; set; }

     public int LeagueRank { get; set; }

     public int ContributionPoints { get; set; }

     public int ContributionRank { get; set; }
#endif

     public byte Unk7 { get; set; }
  }
}

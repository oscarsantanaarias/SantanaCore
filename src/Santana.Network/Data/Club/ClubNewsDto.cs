using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class ClubNewsDto
  {
    public ClubNewsDto()
    {
      Unk2 = "";
      Unk3 = "";
    }

     public int Unk1 { get; set; }

    public string Unk2 { get; set; }

    public string Unk3 { get; set; }
  }
}

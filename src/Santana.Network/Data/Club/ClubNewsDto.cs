using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class ClubNewsDto
  {
    public ClubNewsDto()
    {
      Body = "";
      Date = "";
    }

     public int Category { get; set; }

    public string Body { get; set; }

    public string Date { get; set; }
  }
}


using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class BoardMessageDto
  {
     public int PostId { get; set; }

    public string Unk2 { get; set; }

     public int RowType { get; set; }

     public int AuthorId { get; set; }

     public int ClassIcon { get; set; }

    public string AuthorName { get; set; }

     public int AuthorAccountId { get; set; }

    public string Message { get; set; }

    public string CreatedAt { get; set; }

     public int Unk10 { get; set; }

     public int Unk11 { get; set; }

     public byte IsPublic { get; set; }

     public int ClubId { get; set; }
  }
}

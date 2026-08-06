
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
  [Dto]
  public class BoardMessageDto
  {
     public int PostId { get; set; }

    public string AuthorClubMark { get; set; }

     public int RowType { get; set; }

     public int AuthorId { get; set; }

     public int ClassIcon { get; set; }

    public string AuthorName { get; set; }

     public int AuthorAccountId { get; set; }

    public string Message { get; set; }

    public string CreatedAt { get; set; }

     public int Unk10 { get; set; }

     public int Unk11 { get; set; }

     public byte MembersOnly { get; set; }

     public int AuthorClubId { get; set; }
  }
}

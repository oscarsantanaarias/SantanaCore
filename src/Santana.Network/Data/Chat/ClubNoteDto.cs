
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Chat
{
  [Dto]
  public class ClubNoteDto
  {
    public ClubNoteDto()
    {
      Title = "";
      Message = "";
    }

     public int ClubId { get; set; }

     public byte ToStaff { get; set; }

     public byte ToRegular { get; set; }

     public byte ToNormal { get; set; }

     public byte ToBadManner { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }
  }
}

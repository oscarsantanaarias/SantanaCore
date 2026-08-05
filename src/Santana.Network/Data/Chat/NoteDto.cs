
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Chat
{
  [Dto]
  public class NoteDto
  {
    public NoteDto()
    {
      Sender = "";
      Title = "";
      OpenedGift = true;
    }

     public ulong Id { get; set; }

    public string Sender { get; set; }

     public int MessageType { get; set; }

     public ulong Unk1 { get; set; }

     public ulong Receiver { get; set; }

    public string Title { get; set; }

     public ushort ReadCount { get; set; }

     public byte DaysLeft { get; set; }

     public byte Unk8 { get; set; }

    public bool IsGift
    {
      get => MessageType == 5 || MessageType == 8;
      set => MessageType = value ? 5 : 0;
    }

    public bool OpenedGift
    {
      get => MessageType == 8 || Unk8 != 0;
      set => Unk8 = (byte)(value ? 1 : 0);
    }
  }
}

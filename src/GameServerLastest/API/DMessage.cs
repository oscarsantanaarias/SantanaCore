using System;
using System.Collections.Generic;
using System.Text;

namespace Santana.API
{
  public class DMessage : ByteArray
  {
    public byte[] Buffer => GetBuffer();
    public int Length => Buffer.Length;

    public DMessage()
    {
    }

    public DMessage(ByteArray packet)
      : base(packet)
    {
    }

    public DMessage(byte[] data, int length)
      : base(data, length)
    {
    }

    internal void Write(MessageType obj)
    {
      Write((byte)obj);
    }

    internal bool Read(ref MessageType obj)
    {
      byte tag = 0;
      if (!Read(ref tag))
        return false;
      obj = (MessageType)tag;
      return true;
    }

    internal void Write(DMessage obj)
    {
      Write(obj.Buffer);
    }

    internal void Write(string obj)
    {
      var asciiText = Encoding.ASCII.GetString(Encoding.UTF8.GetBytes(obj));
      Write((byte)1);
      WriteScalar(obj.Length);
      Write(Encoding.ASCII.GetBytes(asciiText));
    }

    internal bool Read(ref string obj)
    {
      long byteCount = 0;
      byte encoding = 0;

      if (!Read(ref encoding) || !ReadScalar(ref byteCount))
        return false;

      if (!CanRead(byteCount))
        return false;

      var payload = new byte[byteCount];
      if (!Read(ref payload, (int)byteCount))
        return false;

      switch (encoding)
      {
        case 1:
          obj = Encoding.ASCII.GetString(payload);
          return true;
        case 2:
          obj = Encoding.Unicode.GetString(payload);
          return true;
        default:
          return false;
      }
    }

    internal enum MessageType : byte
    {
      Ignore,
      Rmi,
      Encrypted,
      Notify
    }
  }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Santana.API
{
  public class ByteArray
  {
    private byte[] _bytes = new byte[0];
    private int _cursorRead;
    private int _cursorWrite;

    public byte[] GetBuffer()
    {
      return (byte[])_bytes.Clone();
    }

    public int ReadOffset => _cursorRead;
    public int WriteOffset => _cursorWrite;

    public ByteArray()
    {
    }

    public ByteArray(ByteArray data)
    {
      _bytes = data._bytes;
    }

    public ByteArray(byte[] data)
    {
      _bytes = (byte[])data.Clone();
    }

    public ByteArray(byte[] data, int length)
    {
      _bytes = new byte[length];
      Array.Copy(data, _bytes, length);
    }

    internal void Write(byte[] obj)
    {
      int end = _cursorWrite + obj.Length;
      if (_bytes.Length <= end)
        Array.Resize(ref _bytes, end);
      Array.Copy(obj, 0, _bytes, _cursorWrite, obj.Length);
      _cursorWrite = end;
    }

    internal void WriteScalar(byte obj)
    {
      WriteScalar((long)obj);
    }

    internal void WriteScalar(short obj)
    {
      WriteScalar((long)obj);
    }

    internal void WriteScalar(int obj)
    {
      WriteScalar((long)obj);
    }

    internal void WriteScalar(long obj)
    {
      if (obj <= sbyte.MaxValue)
      {
        Write((byte)1);
        Write((byte)obj);
      }
      else if (obj <= short.MaxValue)
      {
        Write((byte)2);
        Write((short)obj);
      }
      else if (obj <= int.MaxValue)
      {
        Write((byte)4);
        Write((int)obj);
      }
      else
      {
        Write((byte)8);
        Write(obj);
      }
    }

    internal void Write(bool obj)
    {
      Write(obj ? (byte)1 : (byte)0);
    }

    internal void Write(byte obj)
    {
      var single = new byte[1];
      single[0] = obj;
      Write(single);
    }

    internal void Write(short obj)
    {
      Write(BitConverter.GetBytes(obj));
    }

    internal void Write(int obj)
    {
      Write(BitConverter.GetBytes(obj));
    }

    internal void Write(long obj)
    {
      Write(BitConverter.GetBytes(obj));
    }

    internal void Write(ByteArray obj)
    {
      WriteScalar(obj._cursorWrite);
      Write(obj._bytes);
    }

    protected bool CanRead(long length)
    {
      return length >= 0 && length <= _bytes.Length - _cursorRead;
    }

    internal bool Read(ref ByteArray obj)
    {
      long declaredLen = 0;
      if (!ReadScalar(ref declaredLen))
        return false;
      if (!CanRead(declaredLen))
        return false;

      var chunk = new byte[declaredLen];
      if (Read(ref chunk, chunk.Length))
      {
        obj = new ByteArray(chunk);
        return true;
      }
      return false;
    }

    internal bool Read(ref byte[] obj, int length)
    {
      if (_bytes.Length >= _cursorRead + length)
      {
        var slice = new byte[length];
        Array.Copy(_bytes, _cursorRead, slice, 0, length);
        obj = slice;
        _cursorRead += length;
        return true;
      }
      return false;
    }

    internal bool Read(ref bool obj)
    {
      byte flag = 0;
      var ok = Read(ref flag);
      obj = flag == 1;
      return ok;
    }

    internal bool Read(ref byte obj)
    {
      if (_bytes.Length >= _cursorRead)
      {
        obj = _bytes[_cursorRead];
        _cursorRead += 1;
        return true;
      }
      return false;
    }

    internal bool Read(ref short obj)
    {
      var raw = new byte[2];
      if (Read(ref raw[0])
          && Read(ref raw[1]))
      {
        obj = BitConverter.ToInt16(raw, 0);
        return true;
      }
      return false;
    }

    internal bool Read(ref int obj)
    {
      var raw = new byte[4];
      if (Read(ref raw[0])
          && Read(ref raw[1])
          && Read(ref raw[2])
          && Read(ref raw[3]))
      {
        obj = BitConverter.ToInt32(raw, 0);
        return true;
      }
      return false;
    }

    internal bool Read(ref long obj)
    {
      var raw = new byte[8];
      if (Read(ref raw[0])
          && Read(ref raw[1])
          && Read(ref raw[2])
          && Read(ref raw[3])
          && Read(ref raw[4])
          && Read(ref raw[5])
          && Read(ref raw[6])
          && Read(ref raw[7]))
      {
        obj = BitConverter.ToInt64(raw, 0);
        return true;
      }
      return false;
    }

    internal bool ReadScalar(ref long obj)
    {
      byte as8bit = 0;
      short as16bit = 0;
      var as32bit = 0;
      long as64bit = 0;

      byte sizePrefix = 0;
      if (!Read(ref sizePrefix))
        return false;

      switch (sizePrefix)
      {
        case 8:
          if (!Read(ref as64bit))
            return false;
          obj = as64bit;
          break;
        case 4:
          if (!Read(ref as32bit))
            return false;
          obj = as32bit;
          break;
        case 2:
          if (!Read(ref as16bit))
            return false;
          obj = as16bit;
          break;
        case 1:
          if (!Read(ref as8bit))
            return false;
          obj = as8bit;
          break;
        default:
          return false;
      }
      return true;
    }
  }
}

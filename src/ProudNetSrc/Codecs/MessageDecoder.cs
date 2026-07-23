namespace ProudNetSrc.Codecs
{
  using System.Collections.Generic;
  using System.Linq;
  using SantanaLib.IO;
  using DotNetty.Buffers;
  using DotNetty.Codecs;
  using DotNetty.Transport.Channels;
  using ProudNetSrc.Serialization;
  using ProudNetSrc.Serialization.Messages;
  using ReadOnlyByteBufferStream = SantanaLib.DotNetty.ReadOnlyByteBufferStream;

  internal class MessageDecoder : MessageToMessageDecoder<RecvContext>
  {
    private readonly MessageFactory[] _userMessageFactories;

    public MessageDecoder(MessageFactory[] userMessageFactories)
    {
      _userMessageFactories = userMessageFactories;
    }

    protected override void Decode(IChannelHandlerContext context, RecvContext message, List<object> output)
    {
      var buffer = message.Message as IByteBuffer;
      try
      {
        if (buffer == null)
          return;

        if (buffer.ReadableBytes < 2)
          return;

        var _start = buffer.ReaderIndex;
        var _len = buffer.ReadableBytes;

        using (var r = new ReadOnlyByteBufferStream(buffer, false).ToBinaryReader(false))
        {
          var opCode = r.ReadUInt16();
          var isInternal = opCode >= 64000;
          var factory = isInternal
              ? RmiMessageFactory.Default
              : _userMessageFactories.FirstOrDefault(userFactory => userFactory.ContainsOpCode(opCode));

          if (factory == null)
          {
#if DEBUG
            throw new ProudBadOpCodeException(opCode, buffer.GetIoBuffer());
#else
                        throw new ProudException($"No {nameof(MessageFactory)} found for rmi {opCode}");
#endif
          }

          try
          {
            message.Message = factory.GetMessage(opCode, r);
          }
          catch (System.Exception ex)
          {
            var _snap = new byte[_len];
            buffer.GetBytes(_start, _snap);
            Serilog.Log.Warning("[DECODE-FAIL] op={Op} len={Len} bytes={Hex} err={Err}", opCode, _len, System.BitConverter.ToString(_snap), ex.Message);
            throw;
          }
          if (PacketLog.Enabled && opCode != 11 && opCode != 64019 && opCode != 64001)
            System.Console.WriteLine($"<< inbound rmi #{opCode} decoded as {message.Message.GetType().Name}");
          output.Add(message);
        }
      }
      finally
      {
        buffer?.Release();
      }
    }
  }
}

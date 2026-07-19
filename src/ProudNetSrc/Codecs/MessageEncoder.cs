namespace ProudNetSrc.Codecs
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using SantanaLib.DotNetty;
  using SantanaLib.IO;
  using SantanaLib.Serialization;
  using DotNetty.Codecs;
  using DotNetty.Transport.Channels;
  using ProudNetSrc.Serialization;
  using ProudNetSrc.Serialization.Messages;

  internal class MessageEncoder : MessageToMessageEncoder<SendContext>
  {
    private readonly MessageFactory[] _userMessageFactories;

    public MessageEncoder(MessageFactory[] userMessageFactories)
    {
      _userMessageFactories = userMessageFactories;
    }

    protected override void Encode(IChannelHandlerContext context, SendContext message, List<object> output)
    {
      var type = message.Message.GetType();
      var isInternal = RmiMessageFactory.Default.ContainsType(type);
      var factory = isInternal
          ? RmiMessageFactory.Default
          : _userMessageFactories.FirstOrDefault(userFactory => userFactory.ContainsType(type));

      if (factory == null)
        throw new ProudException($"No {nameof(MessageFactory)} found for message {type.FullName}");

      var opCode = factory.GetOpCode(type);
      if (PacketLog.Enabled && opCode != 1020 && opCode != 64508 && opCode != 1092 && opCode != 16023)
        Console.WriteLine($">> outbound rmi #{opCode} encoded from {type.Name}");
      var buffer = context.Allocator.Buffer(2);
      using (var w = new WriteOnlyByteBufferStream(buffer, false).ToBinaryWriter(false))
      {
        w.Write(opCode);
        Packet.Serialize(w, message.Message);
      }

      message.Message = buffer;
      output.Add(message);
    }
  }
}

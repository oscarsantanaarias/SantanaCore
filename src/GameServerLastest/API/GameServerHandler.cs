using System;
using System.Collections.Generic;
using System.Linq;

using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

using Santana;
using Santana.Network;

using Serilog;
using Serilog.Core;

namespace Santana.API
{
  public class GameServerHandler : ChannelHandlerAdapter
  {
    private static readonly ILogger logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(GameServerHandler));
    private static readonly short Magic = 0x1111;

    public override void ChannelActive(IChannelHandlerContext context)
    {
      base.ChannelActive(context);
      var hello = new DMessage();
      hello.Write(DMessage.MessageType.Notify);
      hello.Write("NetIT-Core");
      SendA(context, hello);
    }

    public override void ChannelRead(IChannelHandlerContext context, object messageData)
    {
      var incoming = messageData as IByteBuffer;
      var raw = new byte[0];
      if (incoming != null) raw = incoming.GetIoBuffer().ToArray();

      var frame = new DMessage(raw, raw.Length);
      short header = 0;
      var payload = new ByteArray();

      if (!frame.Read(ref header) || header != Magic || !frame.Read(ref payload))
        return;

      var body = new DMessage(payload);
      DMessage.MessageType kind = 0;
      if (!body.Read(ref kind))
        return;

      if (kind == DMessage.MessageType.Notify)
        return;

      if (kind != DMessage.MessageType.Rmi)
        return;

      short rmi = 0;
      body.Read(ref rmi);

      if (rmi == 10)
      {
        var online = 0;
        foreach (var session in GameServer.Instance.Sessions.Values)
        {
          var gs = (GameSession)session;
          if (gs.IsLoggedIn())
            online++;
        }

        var reply = new DMessage();

        if (online < 0)
        {
          reply.Write(false);
        }
        else
        {
          reply.Write(true);
          reply.Write(online);
        }

        RmiSend(context, 11, reply);
        return;
      }

      if (rmi == 12)
      {
        long channelId = 0;
        long userId = 0;

        body.Read(ref channelId);
        body.Read(ref userId);

        if (channelId == 0 || userId == 0)
          return;

        var echo = new DMessage();
        echo.Write(channelId);
        echo.Write(userId);

        RmiSend(context, 13, echo);
        return;
      }
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
      base.ExceptionCaught(context, exception);
      logger.Error(exception.StackTrace);
    }

    public void RmiSend(IChannelHandlerContext ctx, short rmiId, DMessage message)
    {
      var rmiframe = new DMessage();
      rmiframe.Write(DMessage.MessageType.Rmi);
      rmiframe.Write(rmiId);
      rmiframe.Write(message);
      SendA(ctx, rmiframe);
    }

    public void SendA(IChannelHandlerContext ctx, DMessage data)
    {
      var coreframe = new DMessage();
      coreframe.Write(Magic);
      coreframe.WriteScalar(data.Length);
      coreframe.Write(data);

      var buffer = Unpooled.Buffer(coreframe.Length);
      buffer.WriteBytes(coreframe.Buffer);
      ctx.WriteAndFlushAsync(buffer);
    }
  }
}

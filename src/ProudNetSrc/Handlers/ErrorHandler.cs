namespace ProudNetSrc.Handlers
{
  using System;
  using System.Net.Sockets;
  using DotNetty.Codecs;
  using DotNetty.Transport.Channels;

  internal class ErrorHandler : ChannelHandlerAdapter
  {
    private readonly ProudServer _server;

    public ErrorHandler(ProudServer server)
    {
      _server = server;
    }

    public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
    {
      if (exception == null)
        return;

      var session = context.Channel.GetAttribute(ChannelAttributes.Session).Get();
      var exc = exception.GetBaseException();

      if (session != null && IsPayloadException(exc))
      {
        Serilog.Log.Warning(exception,
            "Peer {HostId} sent a message whose payload could not be read to the end ({Type}: {Msg}); keeping the link and bouncing it to the server list",
            session.HostId, exc.GetType().Name, exc.Message);
        _server.RaiseError(new ErrorEventArgs(session, exception));
        return;
      }

      if (IsProtocolOrConnectionException(exception) || IsProtocolOrConnectionException(exc))
      {
        Serilog.Log.Warning(exception, "Tearing down peer {HostId} after a transport fault of kind {Type} ({Msg})",
            session?.HostId, exc.GetType().Name, exc.Message);
        session?.CloseAsync();
        return;
      }

      _server.Configuration.Logger?.Error(exception, "Fault escaped the pipeline without a handler");
      _server.RaiseError(new ErrorEventArgs(session, exception));
    }

    private static bool IsPayloadException(Exception e)
    {
      return e is System.IO.EndOfStreamException;
    }

    private static bool IsProtocolOrConnectionException(Exception e)
    {
      return e is SocketException
          || e is ClosedChannelException
          || e is ProudFrameException
          || e is ProudException
          || e is DecoderException
          || e is CorruptedFrameException
          || e is TooLongFrameException
          || e is System.IO.EndOfStreamException;
    }
  }
}

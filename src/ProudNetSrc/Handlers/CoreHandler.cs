namespace ProudNetSrc.Handlers
{
  using System;
  using System.Diagnostics;
  using System.IO;
  using System.Linq;
  using System.Collections.Generic;
  using SantanaLib.Collections.Concurrent;
  using SantanaLib.DotNetty;
  using SantanaLib.DotNetty.Handlers.MessageHandling;
  using SantanaLib.Security.Cryptography;
  using DotNetty.Buffers;
  using DotNetty.Transport.Channels;
  using ProudNetSrc.Codecs;
  using ProudNetSrc.Serialization;
  using ProudNetSrc.Serialization.Messages;
  using ProudNetSrc.Serialization.Messages.Core;

  internal class CoreHandler : ProudMessageHandler
  {
    private readonly ProudServer _server;
    private readonly Lazy<DateTime> _startTime = new Lazy<DateTime>(() => Process.GetCurrentProcess().StartTime);

    public CoreHandler(ProudServer server)
    {
      _server = server;
    }

    [MessageHandler(typeof(RmiMessage))]
    public void RmiMessage(IChannelHandlerContext context, RmiMessage message, RecvContext recvContext)
    {
      recvContext.Message = Unpooled.WrappedBuffer(message.Data);
      context.FireChannelRead(recvContext);
    }

    [MessageHandler(typeof(CompressedMessage))]
    public void CompressedMessage(IChannelHandlerContext context, CompressedMessage message, RecvContext recvContext)
    {
      if (++recvContext.CompressionDepth > 2)
        throw new ProudException("CompressedMessage nesting too deep");

      var decompressed = message.Data.DecompressZLib();
      recvContext.Message = Unpooled.WrappedBuffer(decompressed);
      context.Channel.Pipeline.Context<ProudFrameDecoder>().FireChannelRead(recvContext);
    }

    [MessageHandler(typeof(EncryptedReliableMessage))]
    public void EncryptedReliableMessage(IChannelHandlerContext context, ProudSession session, EncryptedReliableMessage message, RecvContext recvContext)
    {
      var crypt = session.Crypt;
      if (crypt == null)
        return;

      var buffer = context.Allocator.Buffer(message.Data.Length);
      using (var src = new MemoryStream(message.Data))
      using (var dst = new WriteOnlyByteBufferStream(buffer, false))
      {
        crypt.Decrypt(context.Allocator, message.EncryptMode, src, dst, true);
      }

      recvContext.Message = buffer;
      context.Channel.Pipeline.Context<ProudFrameDecoder>().FireChannelRead(recvContext);
    }

    [MessageHandler(typeof(NotifyCSEncryptedSessionKeyMessage))]
    public void NotifyCSEncryptedSessionKeyMessage(ProudServer server, ProudSession session, NotifyCSEncryptedSessionKeyMessage message)
    {
        session.Logger?.Verbose("Key exchange stage - unwrapping the ciphered session key from the peer");
        var secureKey = server.Rsa.Decrypt(message.SecureKey, true);
        session.Crypt = new Crypt(secureKey);
        var fastKey = session.Crypt.AES.Decrypt(message.FastKey);
        session.Crypt.InitializeFastEncryption(fastKey);
        session.SendAsync(new NotifyCSSessionKeySuccessMessage());
     }

    [MessageHandler(typeof(NotifyServerConnectionRequestDataMessage))]
    public void NotifyServerConnectionRequestDataMessage(ProudSession session, NotifyServerConnectionRequestDataMessage message)
    {
      session.Logger?.Verbose("Key exchange stage - inspecting the peer connect payload");
      if (message.InternalNetVersion != Constants.NetVersion ||
          message.Version != _server.Configuration.Version)
      {
        session.Logger?.Warning("Refusing peer: build stamps disagree, peer reports {@ClientVersion} while this host runs {@ServerVersion}",
            new { NetVersion = message.InternalNetVersion, message.Version },
            new { Constants.NetVersion, _server.Configuration.Version });

        session.SendAsync(new NotifyProtocolVersionMismatchMessage());
        session.CloseAsync();
        return;
      }

      _server.AddSession(session);
      session.HandhsakeEvent.Set();
      session.SendAsync(new NotifyServerConnectSuccessMessage(session.HostId, _server.ServerInstanceGuid, session.RemoteEndPoint));
    }

    [MessageHandler(typeof(UnreliablePingMessage))]
    public void UnreliablePingHandler(ProudSession session, UnreliablePingMessage message, RecvContext recvContext)
    {
      session.UnreliablePing = TimeSpan.FromSeconds(message.Ping).TotalMilliseconds;
      if (recvContext.UdpEndPoint != null)
        session.LastUdpPing = DateTimeOffset.Now;

      var ts = DateTime.Now - _startTime.Value;
      session.SendUdpIfAvailableAsync(new UnreliablePongMessage(message.ClientTime, ts.TotalSeconds));
    }

    [MessageHandler(typeof(SpeedHackDetectorPingMessage))]
    public void SpeedHackDetectorPingHandler(ProudSession session)
    {
      session.LastSpeedHackDetectorPing = DateTime.Now;
    }

    [MessageHandler(typeof(ReliableRelay1UnkMessage))]
    public void ReliableRelayUnkHandler(ProudSession session, ReliableRelay1UnkMessage message)
    {
      if (session.P2PGroup == null)
        return;

      if (!session.P2PGroup.Members.ContainsKey(message.Destination.HostId))
        return;

      var target = _server.Sessions.GetValueOrDefault(message.Destination.HostId);
      target?.SendAsync(new ReliableRelay2Message(new RelayDestinationDto(session.HostId, message.Destination.FrameNumber), message.Data));
    }

    private static void LogGameP2P(ProudSession session, byte[] data)
    {
      if (data == null || data.Length < 9 || data[0] != 0x13 || data[1] != 0x57)
        return;
      var prefix = data[2];
      if (prefix != 1 && prefix != 2 && prefix != 4)
        return;
      var coreIdx = 3 + prefix;
      if (coreIdx + 2 >= data.Length || data[coreIdx] != 1)
        return;
      var userOp = data[coreIdx + 1] | (data[coreIdx + 2] << 8);
      if (userOp != 0x4E39)
        return;

      // contenedor crudo con prefix 1: el inner arranca en coreIdx+6; subOp 05 = DamageInfo
      if (data[coreIdx + 3] != 0 || prefix != 1)
        return;
      var innerIdx = coreIdx + 6;
      if (innerIdx + 8 >= data.Length)
        return;

      // 0x05 DamageInfo: GameTime en +4. 0x2A AbilityChangeSync: peer del emisor en +3.
      if (data[innerIdx] == 0x05)
        RelayFrameTracker.ObserveGameTime(BitConverter.ToUInt32(data, innerIdx + 4));
      else if (data[innerIdx] == 0x2A)
        RelayFrameTracker.ObservePeer(session.HostId, BitConverter.ToUInt16(data, innerIdx + 3));
    }

    private static readonly System.Collections.Generic.HashSet<int> _seenP2P = new System.Collections.Generic.HashSet<int>();
    private static void LogArcadeP2P(ProudSession session, byte[] data)
    {
      try
      {
        if (data == null || data.Length < 6)
          return;
        if (data[0] != 0x13 || data[1] != 0x57)
          return;
        var prefix = data[2];
        if (prefix != 1 && prefix != 2 && prefix != 4)
          return;
        var core = 3 + prefix;
        if (core + 2 >= data.Length || data[core] != 1)
          return;
        var userOp = data[core + 1] | (data[core + 2] << 8);
        var zi = -1;
        for (var i = core + 3; i < data.Length - 1 && i < core + 14; i++)
        {
          if (data[i] == 0x78 && (data[i + 1] == 0xDA || data[i + 1] == 0x9C || data[i + 1] == 0x01))
          {
            zi = i;
            break;
          }
        }
        int op = -1;
        string bodyHead = "(raw)";
        if (zi >= 0)
        {
          using (var input = new System.IO.MemoryStream(data, zi, data.Length - zi))
          using (var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress))
          using (var output = new System.IO.MemoryStream())
          {
            zlib.CopyTo(output);
            var body = output.ToArray();
            if (body.Length > 0)
            {
              op = body[0];
              bodyHead = System.BitConverter.ToString(body, 0, System.Math.Min(24, body.Length));
            }
          }
        }
        else if (core + 6 < data.Length)
        {
          op = data[core + 6];
        }
        var key = ((userOp & 0xFFFF) << 8) | (op & 0xFF);
        lock (_seenP2P)
        {
          if (!_seenP2P.Add(key))
            return;
        }
        Serilog.Log.Information("[P2P-NEW] host={H} userOp=0x{U:X4} zlib={Z} bodyOp=0x{Op:X2} body={B} len={L}", session.HostId, userOp, zi >= 0, op < 0 ? 0 : op, bodyHead, data.Length);
      }
      catch { }
    }

    public static bool RewriteMonsterOwners = true;

    private static void WriteLe(byte[] buf, int off, int count, int value)
    {
      for (var i = 0; i < count; i++)
        buf[off + i] = (byte)(value >> (8 * i));
    }

    private static byte[] RepointDeadMonsterOwners(ProudSession session, byte[] data)
    {
      try
      {
        if (!RewriteMonsterOwners || data == null || data.Length < 250 || data[0] != 0x13 || data[1] != 0x57)
          return null;
        var prefix = data[2];
        if (prefix != 1 && prefix != 2 && prefix != 4)
          return null;
        var core = 3 + prefix;
        if (data[core] != 1 || (data[core + 1] | (data[core + 2] << 8)) != 0x4E3B)
          return null;

        var senderAcc = RelayFrameTracker.AccountOf(session.HostId);
        var senderPeer = RelayFrameTracker.RefereePeerOf(senderAcc);
        if (senderPeer == 0)
          senderPeer = RelayFrameTracker.PeerOf(session.HostId, 0);
        if (senderPeer == 0 || senderAcc == 0)
          return null;

        var p = core + 3;
        var lenPrefix = data[p + 4];
        var zlibStart = p + 5 + lenPrefix;
        byte[] body;
        using (var input = new MemoryStream(data, zlibStart, data.Length - zlibStart))
        using (var zlib = new System.IO.Compression.ZLibStream(input, System.IO.Compression.CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
          zlib.CopyTo(output);
          body = output.ToArray();
        }

        var changed = false;
        for (var i = 0; i + 8 <= body.Length; i++)
        {
          if (body[i + 4] != 0 || body[i + 5] != 0)
            continue;
          var peer = (ushort)(body[i + 6] | (body[i + 7] << 8));
          var slot = (peer >> 3) & 0x1F;
          if ((peer & 7) != 1 || slot < 3 || slot > 8 || (peer >> 8) > 8)
            continue;
          var acc = BitConverter.ToUInt32(body, i);
          if (acc == 0 || peer == senderPeer)
            continue;
          WriteLe(body, i, 4, (int)senderAcc);
          body[i + 6] = (byte)senderPeer;
          body[i + 7] = (byte)(senderPeer >> 8);
          changed = true;
        }
        if (!changed)
          return null;

        byte[] recompressed;
        using (var output = new MemoryStream())
        {
          using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, true))
            zlib.Write(body, 0, body.Length);
          recompressed = output.ToArray();
        }

        var frameLen = zlibStart + recompressed.Length;
        if (recompressed.Length >= (1 << (8 * lenPrefix)) || frameLen - core >= (1 << (8 * prefix)))
          return null;

        var patched = new byte[frameLen];
        Buffer.BlockCopy(data, 0, patched, 0, zlibStart);
        Buffer.BlockCopy(recompressed, 0, patched, zlibStart, recompressed.Length);
        WriteLe(patched, 3, prefix, frameLen - core);
        WriteLe(patched, p + 5, lenPrefix, recompressed.Length);
        return patched;
      }
      catch
      {
        return null;
      }
    }

    [MessageHandler(typeof(ReliableRelay1Message))]
    public void ReliableRelayHandler(ProudSession session, ReliableRelay1Message message)
    {
      if (session.P2PGroup == null)
        return;

      LogGameP2P(session, message.Data);
      LogArcadeP2P(session, message.Data);
      var patched = RepointDeadMonsterOwners(session, message.Data);
      if (patched != null)
        message.Data = patched;

      foreach (var destination in message.Destination.Where(d => d.HostId != session.HostId))
      {
        if (session.P2PGroup == null) continue;

        if (!session.P2PGroup.Members.ContainsKey(destination.HostId)) continue;

        RelayFrameTracker.Observe(session.HostId, destination.HostId, destination.FrameNumber);

        var target = _server.Sessions.GetValueOrDefault(destination.HostId);
        target?.SendAsync(new ReliableRelay2Message(new RelayDestinationDto(session.HostId, destination.FrameNumber), message.Data));
      }
    }

    [MessageHandler(typeof(UnreliableRelay1Message))]
    public void UnreliableRelayHandler(ProudSession session, UnreliableRelay1Message message)
    {
      LogArcadeP2P(session, message.Data);
      foreach (var destination in message.Destination.Where(id => id != session.HostId))
      {
        if (session.P2PGroup == null)
          continue;

        if (!session.P2PGroup.Members.ContainsKey(destination))
          continue;

        var target = _server.Sessions.GetValueOrDefault(destination);
        target?.SendUdpIfAvailableAsync(new UnreliableRelay2Message(session.HostId, message.Data));
      }
    }

    [MessageHandler(typeof(ServerHolepunchMessage))]
    public void ServerHolepunch(ProudSession session, ServerHolepunchMessage message)
    {
      session.Logger?.Debug("Peer is probing the server udp path, probe payload {@Message}", message);
      if (session.P2PGroup == null || !_server.UdpSocketManager.IsRunning ||
          session.HolepunchMagicNumber != message.MagicNumber)
        return;

      session.SendUdpAsync(new ServerHolepunchAckMessage(session.HolepunchMagicNumber, session.UdpEndPoint));
    }

    [MessageHandler(typeof(NotifyHolepunchSuccessMessage))]
    public void NotifyHolepunchSuccess(ProudSession session, NotifyHolepunchSuccessMessage message)
    {
      session.Logger?.Debug("Peer reports the server udp path is now open, report {@Message}", message);
      if (session.P2PGroup == null || !_server.UdpSocketManager.IsRunning ||
          session.HolepunchMagicNumber != message.MagicNumber)
        return;

      session.LastUdpPing = DateTimeOffset.Now;
      session.UdpEnabled = true;
      session.UdpLocalEndPoint = message.LocalEndPoint;
      session.SendUdpAsync(new NotifyClientServerUdpMatchedMessage(message.MagicNumber));
    }

    [MessageHandler(typeof(PeerUdp_ServerHolepunchMessage))]
    public void PeerUdp_ServerHolepunch(ProudSession session, PeerUdp_ServerHolepunchMessage message, RecvContext recvContext)
    {
      session.Logger?.Debug("Peer asks the server to relay a udp probe toward another member, probe payload {@Message}", message);
      if (!session.UdpEnabled || !_server.UdpSocketManager.IsRunning)
        return;

      var target = session.P2PGroup?.Members.GetValueOrDefault(message.HostId)?.Session;
      if (target == null || !target.UdpEnabled)
        return;

      session.SendUdpAsync(new PeerUdp_ServerHolepunchAckMessage(message.MagicNumber, recvContext.UdpEndPoint, target.HostId));
    }

    [MessageHandler(typeof(PeerUdp_NotifyHolepunchSuccessMessage))]
    public void PeerUdp_NotifyHolepunchSuccess(ProudSession session, PeerUdp_NotifyHolepunchSuccessMessage message)
    {
      session.Logger?.Debug("Peer confirms a udp path to another member is up, report {@Message}", message);
      if (!session.UdpEnabled || !_server.UdpSocketManager.IsRunning)
        return;

      var remotePeer = session.P2PGroup?.Members.GetValueOrDefault(session.HostId);

      var connectionState = remotePeer?.ConnectionStates?.GetValueOrDefault(message.HostId);
      if (connectionState == null)
        return;

      connectionState.PeerUdpHolepunchSuccess = true;
      connectionState.LocalEndPoint = message.LocalEndPoint;
      connectionState.EndPoint = message.EndPoint;

      var connectionStateB = connectionState.RemotePeer?.ConnectionStates.GetValueOrDefault(session.HostId);
      if (connectionStateB?.PeerUdpHolepunchSuccess ?? false)
      {
        remotePeer.SendAsync(new RequestP2PHolepunchMessage(message.HostId, connectionStateB.LocalEndPoint, connectionStateB.EndPoint));
        connectionState.RemotePeer.SendAsync(new RequestP2PHolepunchMessage(session.HostId, connectionState.LocalEndPoint, connectionState.EndPoint));
      }
    }
  }
}

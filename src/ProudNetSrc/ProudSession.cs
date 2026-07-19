namespace ProudNetSrc
{
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Net;
    using System.Threading.Tasks;
    using SantanaLib.Threading.Tasks;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using ProudNetSrc.Serialization.Messages;
    using ProudNetSrc.Serialization.Messages.Core;
    using Serilog;

    public class ProudSession : IDisposable
    {
        private readonly object _disposeMutex = new object();
        private volatile bool _isDisposing;
        private bool _disposed;
        private IPEndPoint _udpEndPoint;
        private IPEndPoint _udpLocalEndPoint;

        public ProudSession(uint hostId, IChannel channel, ProudServer server)
        {
            try
            {
                var ip = channel.RemoteAddress.ToString().Substring(0, channel.RemoteAddress.ToString().LastIndexOf(":")).Replace("[::ffff:", "").Replace("]", "");

                HostId = hostId;
                Server = server;
                Channel = (ISocketChannel)channel;
                HandhsakeEvent = new AsyncManualResetEvent();
                ConnectDate = DateTimeOffset.Now;

                var remoteEndPoint = (IPEndPoint)Channel.RemoteAddress;
                RemoteEndPoint = new IPEndPoint(remoteEndPoint.Address.MapToIPv4(), remoteEndPoint.Port);

                var localEndPoint = (IPEndPoint)Channel.LocalAddress;
                LocalEndPoint = new IPEndPoint(localEndPoint.Address.MapToIPv4(), localEndPoint.Port);

                Logger = Server.Configuration.Logger?
                    .ForContext("HostId", HostId)
                    .ForContext("EndPoint", remoteEndPoint.ToString());

                HandleLock = new MaxUseLock(4);
            }
            catch { }
        }

        public ProudServer Server { get; }
        public ISocketChannel Channel { get; }
        public bool IsConnected => Channel.Open && Channel.Active;
        public bool Authenticated { get; set; }
        public bool XbnSent { get; set; }
        public DateTimeOffset ConnectDate { get; set; }

        public IPEndPoint RemoteEndPoint { get; }
        public IPEndPoint LocalEndPoint { get; }

        public uint HostId { get; }
        public P2PGroup P2PGroup { get; internal set; }

        public IPEndPoint UdpEndPoint
        {
            get => _udpEndPoint;
            internal set
            {
                Logger = Logger?.ForContext("UdpEndPoint", value?.ToString());
                _udpEndPoint = value;
            }
        }

        public IPEndPoint UdpLocalEndPoint
        {
            get => _udpLocalEndPoint;
            internal set
            {
                Logger = Logger?.ForContext("UdpLocalEndPoint", value?.ToString());
                _udpLocalEndPoint = value;
            }
        }

        internal ILogger Logger { get; private set; }
        internal bool UdpEnabled { get; set; }
        internal ushort UdpSessionId { get; set; }
        internal Crypt Crypt { get; set; }
        internal DateTime LastSpeedHackDetectorPing { get; set; }
        internal AsyncManualResetEvent HandhsakeEvent { get; set; }
        private Guid _holepunchMagicNumber;
        internal Guid HolepunchMagicNumber
        {
            get => _holepunchMagicNumber;
            set
            {
                if (_holepunchMagicNumber != Guid.Empty)
                    Server.SessionsByMagic.TryRemove(_holepunchMagicNumber, out _);
                _holepunchMagicNumber = value;
                if (value != Guid.Empty)
                    Server.SessionsByMagic[value] = this;
            }
        }
        internal UdpSocket UdpSocket { get; set; }

        public MaxUseLock HandleLock { get; }
        public double UnreliablePing { get; internal set; }
        internal DateTimeOffset LastUdpPing { get; set; }

        internal int UdpWindowStart { get; set; }
        internal int UdpPacketCount { get; set; }
        internal SessionState State { get; set; }
        public void Dispose() => Task.Run(CloseAsync);

        public Task SendAsync(object message)
        {
            return (_disposed || !IsConnected || !Channel.IsWritable)
                ? Task.CompletedTask
                : SendAsync(message, SendOptions.ReliableSecure);
        }

        public Task SendAsync(object message, SendOptions options)
        {
            try
            {
                var typeName = message?.GetType().Name ?? "null";
                if (typeName.IndexOf("Ack", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var stack = new StackTrace(false).ToString();
                    if (stack.IndexOf("ClubService", StringComparison.Ordinal) >= 0)
                        Console.WriteLine($"~ club reply path: via {GetType().Name}, peer {HostId}, payload {typeName}, channel accepting writes: {Channel?.IsWritable ?? false}");
                }
            }
            catch
            {
            }

            try
            {
                var n = false ? (message?.GetType().Name ?? "null") : null;
                if (n != null &&
                    n.IndexOf("TimeSync", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Pong", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Ping", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Relay2", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("Holepunch", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("P2P", StringComparison.Ordinal) < 0 &&
                    n.IndexOf("RecycleComplete", StringComparison.Ordinal) < 0)
                {
                    Console.WriteLine($">> queued {n} toward peer {HostId}");
                }
            }
            catch
            {
            }

            Logger?.Verbose("Handing {MessageType} to the pipeline under delivery flags {@Options}", message.GetType().Name, options);
            return (_disposed || !IsConnected || !Channel.IsWritable)
                ? Task.CompletedTask
                : Channel.WriteAndFlushAsyncEx(new SendContext(message, options));
        }

        internal Task SendAsync(IMessage message)
        {
            return (_disposed || !IsConnected || !Channel.IsWritable)
                ? Task.CompletedTask
                : SendAsync(message, SendOptions.Reliable);
        }

        internal Task SendAsync(ICoreMessage message)
        {
            return (_disposed || !IsConnected || !Channel.IsWritable)
                ? Task.CompletedTask
                : Channel.Pipeline.Context("coreHandler").WriteAndFlushAsyncEx(message);
        }

        internal Task SendUdpIfAvailableAsync(ICoreMessage message)
        {
            var log = Logger?.ForContext("MessageType", message.GetType().Name);
            if (_disposed || !IsConnected || !Channel.IsWritable)
                return Task.CompletedTask;

            if (UdpEnabled && UdpSocket != null && UdpEndPoint != null)
            {
                return UdpSocket.SendAsync(message, UdpEndPoint);
            }

            return SendAsync(message);
        }

        internal Task SendUdpAsync(ICoreMessage message)
        {
            Logger?.Verbose("Emitting internal frame {MessageType} over the datagram path", message.GetType().Name);

            return (_disposed || !IsConnected || !Channel.IsWritable || UdpSocket == null || UdpEndPoint == null)
                ? Task.CompletedTask
                : UdpSocket.SendAsync(message, UdpEndPoint);
        }

        public Task SendP2PRelay(uint sourceHostId, byte[] data)
        {
            return SendUdpIfAvailableAsync(new Serialization.Messages.Core.UnreliableRelay2Message(sourceHostId, data));
        }

        protected virtual Task CloseInternalAsync()
        {
            return Task.CompletedTask;
        }

        public async Task CloseAsync()
        {
            try
            {
                if (Channel == null)
                    return;
                if (_disposed || !Channel.Open || !Channel.Active)
                    return;

                lock (_disposeMutex)
                {
                    if (_isDisposing || _disposed)
                        return;

                    _isDisposing = true;
                }

                await CloseInternalAsync();

                Logger?.Verbose("Peer teardown in progress, disposing crypto and channel state");

                Crypt?.Dispose();

                lock (_disposeMutex)
                {
                    _disposed = true;
                    _isDisposing = false;
                }

                if (Channel != null)
                    await Channel.CloseAsync();
            }
            catch(ClosedChannelException ex)
            {
                Logger.Error(ex.ToString());
            }
            catch (ChannelException ex)
            {
                Logger.Error(ex.ToString());
            }
        }
    }
}

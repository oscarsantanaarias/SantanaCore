using System;
using System.Threading;
using System.Threading.Tasks;
using Foundatio.Messaging;
using ProudNetSrc;
using ProudNetSrc.Handlers;
using ProudNetSrc.Serialization;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using Santana.Ipc;
using Santana.Network.Message.Relay;
using Serilog;
using Serilog.Core;

namespace Santana.Relay
{
    internal class RelayHost : ProudServer
    {
        private static readonly ILogger Log_ =
            Log.ForContext(Constants.SourceContextPropertyName, "Relay");

        private RelayHost(Configuration config) : base(config) { }

        public static RelayHost Instance { get; private set; }
        public static RelayRoomManager Rooms { get; private set; }
        public static RelayPlayerManager Players { get; private set; }

        public static void Initialize(Configuration config)
        {
            if (Instance != null)
                throw new InvalidOperationException("El relay ya esta inicializado");

            config.Version = new Guid("{a43a97d1-9ec7-495e-ad5f-8fe45fde1151}");
            config.MessageFactories = new MessageFactory[] { new RelayMessageFactory() };
            config.SessionFactory = new RelaySessionFactory();

            bool NotYetLoggedIn(RelaySession session) => session.Player == null;

            config.MessageHandlers = new IMessageHandler[]
            {
                new RelayMessageRouter<RelaySession>()
                    .AddHandler(new RelayAuthHandler())
                    .RegisterRule<CRequestLoginMessage>(NotYetLoggedIn)
            };

            Instance = new RelayHost(config);
            Players = new RelayPlayerManager();
            Rooms = new RelayRoomManager(Instance);
        }

        protected override void OnDisconnected(ProudSession session)
        {
            ProudNetSrc.RelayFrameTracker.Forget(session.HostId);
            try
            {
                if (session is RelaySession relay && relay.Player != null)
                {
                    var plr = relay.Player;
                    plr.Room?.Leave(plr);
                    Players.Remove(plr.Account.Id);
                    Log_.Information("Dropped {nick} (account {id}) from the forwarding roster", plr.Account.Nickname, plr.Account.Id);
                    relay.Player = null;
                }
            }
            catch (Exception ex)
            {
                Log_.Error(ex, "Could not fully release the resources of a departing peer");
            }

            base.OnDisconnected(session);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            Log_.Error(e.Exception, "Forwarding node reported a fault");
            base.OnError(e);
        }
    }

    internal class RelayAuthHandler : ProudMessageHandler
    {
        private static readonly ILogger Log_ =
            Log.ForContext(Constants.SourceContextPropertyName, "Relay");

        [MessageHandler(typeof(CRequestLoginMessage))]
        public async Task LoginHandler(RelaySession session, CRequestLoginMessage message)
        {
            RelayLoginResponse answer;
            try
            {
                answer = await Ipc.Ipc.Bus.PublishRequestAsync<RelayLoginRequest, RelayLoginResponse>(
                    new RelayLoginRequest
                    {
                        AccountId = message.AccountId,
                        Address = session.RemoteEndPoint?.Address?.ToString(),
                        ServerId = message.RoomLocation.ServerId,
                        ChannelId = message.RoomLocation.ChannelId,
                        RoomId = message.RoomLocation.RoomId
                    });
            }
            catch (Exception ex)
            {
                Log_.Error(ex, "No reply came back from the game host while validating account {acc}", message.AccountId);
                await session.SendAsync(new SNotifyLoginResultMessage(1));
                return;
            }

            if (answer?.OK != true || answer.Account == null)
            {
                Log_.Warning("Game host refused the credentials presented for account {acc}", message.AccountId);
                await session.SendAsync(new SNotifyLoginResultMessage(1));
                return;
            }

            if (RelayHost.Players[answer.Account.Id] != null)
            {
                Log_.Warning("Account {acc} already holds an open forwarding slot, rejecting the duplicate", answer.Account.Id);
                await session.SendAsync(new SNotifyLoginResultMessage(2));
                return;
            }

            var plr = new RelayPlayer(session, answer.Account);
            session.Player = plr;
            RelayHost.Players.Add(plr);

            RelayHost.Rooms.GetOrCreate(message.RoomLocation.RoomId).Join(plr);

            Log_.Information("Admitted {nick} (account {id}), now forwarding for match {room}",
                plr.Account.Nickname, plr.Account.Id, message.RoomLocation.RoomId);

            await session.SendAsync(new SNotifyLoginResultMessage(0));
        }
    }

    internal static class RelayIpc
    {
        private static readonly ILogger Log_ =
            Log.ForContext(Constants.SourceContextPropertyName, "Relay");

        private static CancellationTokenSource _shutdown;

        public static async Task StartAsync()
        {
            _shutdown = new CancellationTokenSource();
            await Ipc.Ipc.Bus.SubscribeAsync<PlayerDisconnectedMessage>(OnPlayerDisconnected, _shutdown.Token);
            await Ipc.Ipc.Bus.SubscribeAsync<PlayerLeftRoomMessage>(OnPlayerLeftRoom, _shutdown.Token);
            await Ipc.Ipc.Bus.SubscribeAsync<RelayKillPlayerMessage>(OnKillPlayer, _shutdown.Token);
        }

        // Bloque de dano P2P capturado de un golpe real (sub-mensaje DamageInfo, subOp 0x05, 76 bytes).
        // Entre golpes solo cambian Target(+1), GameTime(+4) y Source(+8); el resto (rotacion,
        // posicion, dano, flags) se reusa tal cual: el cliente solo resta HP hasta matar.
        private static readonly byte[] DamageTemplate =
        {
            0x05, 0x39, 0x00, 0x3D, 0xB7, 0x8B, 0xD4, 0x00, 0x21, 0x00, 0x12, 0x8C, 0x32, 0x31, 0x24,
            0xD5, 0xA0, 0x81, 0xA2, 0x82, 0x13, 0xED, 0x19, 0x24, 0x14, 0x00, 0x00, 0x51, 0x00, 0x02,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x04, 0x00, 0x00, 0x00, 0x28,
            0x12, 0x16, 0x00, 0x00, 0x00, 0x00, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00
        };

        // GameTime del golpe real capturado; se incrementa por golpe para no duplicar.
        private const uint BaseGameTime = 0x00D48BB7;

        private static byte[] BuildDamageFrame(ushort targetPeerId, ushort sourcePeerId, uint gameTime, byte icon)
        {
            var inner = (byte[])DamageTemplate.Clone();
            if (icon != 0)
                inner[3] = icon;
            inner[1] = (byte)(targetPeerId & 0xFF);
            inner[2] = (byte)(targetPeerId >> 8);
            inner[4] = (byte)(gameTime & 0xFF);
            inner[5] = (byte)((gameTime >> 8) & 0xFF);
            inner[6] = (byte)((gameTime >> 16) & 0xFF);
            inner[7] = (byte)((gameTime >> 24) & 0xFF);
            inner[8] = (byte)(sourcePeerId & 0xFF);
            inner[9] = (byte)(sourcePeerId >> 8);

            // payload = [Rmi=01][userOp 0x4E39][flag crudo=00][scalar innerLen][inner]
            var payload = new byte[6 + inner.Length];
            payload[0] = 0x01;
            payload[1] = 0x39;
            payload[2] = 0x4E;
            payload[3] = 0x00;
            payload[4] = 0x01;
            payload[5] = (byte)inner.Length;
            Array.Copy(inner, 0, payload, 6, inner.Length);

            // frame = [NetMagic 13 57][scalar payloadLen][payload]
            var frame = new byte[4 + payload.Length];
            frame[0] = 0x13;
            frame[1] = 0x57;
            frame[2] = 0x01;
            frame[3] = (byte)payload.Length;
            Array.Copy(payload, 0, frame, 4, payload.Length);
            return frame;
        }

        private static async Task OnKillPlayer(RelayKillPlayerMessage message)
        {
            Log_.Information("IPC kill recibido: target {t} source {s} peers {tp}/{sp}",
                message.TargetAccountId, message.SourceAccountId, message.TargetPeerId, message.SourcePeerId);

            var target = RelayHost.Players[message.TargetAccountId];
            var source = RelayHost.Players[message.SourceAccountId];
            if (target?.Session == null || source?.Session == null)
            {
                Log_.Warning("Kill abortado: target={t} source={s} (alguno no esta en el relay)",
                    target?.Session != null, source?.Session != null);
                return;
            }

            // el peer id que manda el GameServer tiene Id=0 fijo; el vivo lo sabe el relay
            var targetPeer = ProudNetSrc.RelayFrameTracker.PeerOf(target.Session.HostId, message.TargetPeerId);
            var sourcePeer = ProudNetSrc.RelayFrameTracker.PeerOf(source.Session.HostId, message.SourcePeerId);
            Log_.Information("peers vivos: target {t} (ipc {ti}) source {s} (ipc {si})",
                targetPeer, message.TargetPeerId, sourcePeer, message.SourcePeerId);

            var hits = message.Hits < 1 ? 1 : message.Hits;
            for (var i = 0; i < hits; i++)
            {
                var frameNo = ProudNetSrc.RelayFrameTracker.Next(source.Session.HostId, target.Session.HostId);
                var frame = BuildDamageFrame(targetPeer, sourcePeer, ProudNetSrc.RelayFrameTracker.GameTimeNow(BaseGameTime) + (uint)(i * 60), message.Icon);
                target.Session.SendP2PRelayReliable(source.Session.HostId, frameNo, frame);
                await Task.Delay(80);
            }

            Log_.Information("Kill inyectado: target {t} <- source {s}, {n} golpes",
                message.TargetAccountId, message.SourceAccountId, hits);
        }

        public static void Stop() => _shutdown?.Cancel();

        private static Task OnPlayerLeftRoom(PlayerLeftRoomMessage message)
        {
            var plr = RelayHost.Players[message.AccountId];
            if (plr == null)
                return Task.CompletedTask;

            plr.Room?.Leave(plr);
            plr.Room = null;
            RelayHost.Players.Remove(message.AccountId);

            return Task.CompletedTask;
        }

        private static Task OnPlayerDisconnected(PlayerDisconnectedMessage message)
        {
            RelayHost.Players[message.AccountId]?.Disconnect();
            return Task.CompletedTask;
        }
    }
}

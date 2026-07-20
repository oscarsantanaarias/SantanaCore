using System.Threading;
using System.Threading.Tasks;
using Foundatio.Messaging;
using Santana.Ipc;
using Serilog;
using Serilog.Core;

namespace Santana.Network.Services
{
    internal static class IpcService
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, "Ipc");

        private static CancellationTokenSource _shutdown;

        public static async Task StartAsync()
        {
            if (!Santana.Ipc.Ipc.IsEnabled)
                return;

            _shutdown = new CancellationTokenSource();
            var bus = Santana.Ipc.Ipc.Bus;

            await bus.SubscribeToRequestAsync<RelayLoginRequest, RelayLoginResponse>(OnRelayLogin, _shutdown.Token);

            Logger.Information("Relay request bus subscribed; awaiting inbound handshakes");
        }

        public static void Stop()
        {
            _shutdown?.Cancel();
        }

        private static Task<RelayLoginResponse> OnRelayLogin(RelayLoginRequest request)
        {
            var plr = GameServer.Instance.PlayerManager[request.AccountId];
            if (plr?.Account == null ||
                plr.Channel == null ||
                plr.Room == null ||
                request.ServerId != Config.Instance.Id ||
                plr.Channel.Id != request.ChannelId ||
                plr.Room.Id != request.RoomId)
            {
                Logger.Warning("Handshake from relay denied; {acc} does not match a live channel/room binding", request.AccountId);
                return Task.FromResult(new RelayLoginResponse(false, null));
            }

            Logger.Information("Relay handshake accepted for {acc} bound to room {room}; connecting flag prior to clear was {was}",
                request.AccountId, plr.Room.Id, plr.RoomInfo.IsConnecting);
            plr.RoomInfo.IsConnecting = false;
            plr.Room.OnPlayerJoined(new RoomPlayerEventArgs(plr));
            Logger.Information("Connecting flag now reads {now} once the join event settled", plr.RoomInfo.IsConnecting);

            return Task.FromResult(new RelayLoginResponse(true,
                new RelayAccountInfo(plr.Account.Id, plr.Account.Nickname)));
        }

        public static void NotifyPlayerLeftRoom(ulong accountId)
        {
            if (!Santana.Ipc.Ipc.IsEnabled)
                return;

            Santana.Ipc.Ipc.Bus.PublishAsync(new PlayerLeftRoomMessage(accountId));
        }

        public static void NotifyPlayerDisconnected(ulong accountId)
        {
            if (!Santana.Ipc.Ipc.IsEnabled)
                return;

            Santana.Ipc.Ipc.Bus.PublishAsync(new PlayerDisconnectedMessage(accountId));
        }
    }
}

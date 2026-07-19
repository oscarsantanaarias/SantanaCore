namespace ProudNetSrc
{
    using System.Collections.Concurrent;

    // ponytail: dict global sin expiry; las entradas mueren con el proceso.
    public static class RelayFrameTracker
    {
        private static readonly ConcurrentDictionary<(uint From, uint To), uint> Last =
            new ConcurrentDictionary<(uint, uint), uint>();

        public static void Observe(uint from, uint to, uint frameNumber) => Last[(from, to)] = frameNumber;

        public static uint Next(uint from, uint to) => Last.AddOrUpdate((from, to), 1u, (_, v) => v + 1);

        // GameTime = reloj P2P sincronizado del grupo; lo tomamos de los golpes reales
        // que pasan por el relay y lo extrapolamos con el reloj de pared.
        private static uint _gameTime;
        private static int _gameTimeTick;

        public static void ObserveGameTime(uint gameTime)
        {
            _gameTime = gameTime;
            _gameTimeTick = System.Environment.TickCount;
        }

        // El byte Id del PeerId sube cada vez que el jugador re-entra a la sala; el server lo
        // tiene hardcodeado en 0 (Room.cs), asi que el real se aprende del trafico del propio peer.
        private static readonly ConcurrentDictionary<uint, ushort> Peers =
            new ConcurrentDictionary<uint, ushort>();

        public static void ObservePeer(uint hostId, ushort peerId) => Peers[hostId] = peerId;

        public static ushort PeerOf(uint hostId, ushort fallback) =>
            Peers.TryGetValue(hostId, out var p) ? p : fallback;

        // Los HostId se reasignan al reconectar; sin esto devolvemos el peer del inquilino anterior.
        public static void Forget(uint hostId)
        {
            Peers.TryRemove(hostId, out _);
            foreach (var key in Last.Keys)
            {
                if (key.From == hostId || key.To == hostId)
                    Last.TryRemove(key, out _);
            }
        }

        public static uint GameTimeNow(uint fallback) =>
            _gameTime == 0 ? fallback : _gameTime + (uint)(System.Environment.TickCount - _gameTimeTick);
    }
}

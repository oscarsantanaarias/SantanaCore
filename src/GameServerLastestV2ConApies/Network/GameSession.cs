using DotNetty.Transport.Channels;
using ProudNetSrc;

namespace Santana.Network
{
    internal class GameSession : ProudSession
    {

        public GameSession(uint hostId, IChannel channel, ProudServer server)
            : base(hostId, channel, server)
        {
            UpdateShop = false;
            UpdateRandomShop = false;
            ValidRes = 0;
            Player = null;
        }

        public Player Player { get; set; }
        public bool UpdateShop;
        public bool UpdateRandomShop;
        public byte ValidRes;
    }

    internal class GameSessionFactory : ISessionFactory
    {
        public ProudSession Create(uint hostId, IChannel channel, ProudServer server)
        {
            return new GameSession(hostId, channel, server);
        }
    }
}

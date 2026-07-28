using DotNetty.Transport.Channels;
using ProudNetSrc;

namespace Santana.Network
{
  internal class ChatSession : ProudSession
  {
    public ChatSession(uint hostId, IChannel channel, ProudServer server)
        : base(hostId, channel, server)
    {
    }

    public GameSession GameSession { get; set; }
    public Player Player => GameSession.Player;
    public ulong LastReadRequestMailId { get; set; }
  }

  internal class ChatSessionFactory : ISessionFactory
  {
    public ProudSession Create(uint hostId, IChannel channel, ProudServer server)
    {
      return new ChatSession(hostId, channel, server);
    }
  }
}

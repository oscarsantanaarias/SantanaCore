using System;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Club;
using Santana.Network.Message.Game;
using Santana.Network.Services;
using ProudNetSrc;
using ProudNetSrc.Serialization;
using Serilog;
using Serilog.Core;

namespace Santana.Network
{
  internal class ChatServer : ProudServer
  {
    private static readonly ILogger
        Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(ChatServer));

    private ChatServer(Configuration config)
        : base(config)
    {
    }

    public static ChatServer Instance { get; private set; }

    public static void Initialize(Configuration config)
    {
      if (Instance != null)
        throw new InvalidOperationException("Server is already initialized");

      config.Version = new Guid("{97d36acf-8cc0-4dfb-bcc9-97cab255e2bc}");
      config.MessageFactories = new MessageFactory[] { new ChatMessageFactory(), new ClubMessageFactory() };
      config.SessionFactory = new ChatSessionFactory();

      bool RequireAuth(ChatSession session)
      {
        return session.IsLoggedIn();
      }

      bool RequireNoAuth(ChatSession session)
      {
        return !session.IsLoggedIn();
      }

      bool RequireChannel(ChatSession session)
      {
        return session.Player.Channel != null;
      }

      config.MessageHandlers = new IMessageHandler[]
      {
                new MessageHandler<ChatSession>()
                    .AddHandler(new AuthService())
                    .AddHandler(new CommunityService())
                    .AddHandler(new ChannelService())
                    .AddHandler(new MSGService())
                    .AddHandler(new ClubService())
                    .AddHandler(new UnusedService())
                    .RegisterRule<LoginReqMessage>(RequireNoAuth)
                    .RegisterRule<CombiCheckNameReqMessage>(RequireAuth)
                    .RegisterRule<CombiActionReqMessage>(RequireAuth)
                    .RegisterRule<RoomInvitationPlayerReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<UserDataOneReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<DenyActionReqMessage>(RequireAuth)
                    .RegisterRule<FriendActionReqMessage>(RequireAuth)
                    .RegisterRule<MessageChatReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<MessageWhisperChatReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<NoteListReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<NoteReadReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<NoteDeleteReqMessage>(RequireAuth, RequireChannel)
                    .RegisterRule<NoteSendReqMessage>(RequireAuth, RequireChannel)
      };
#if DEBUG
      config.Logger = Logger;
#endif
      Instance = new ChatServer(config);
    }

    #region Events

    protected override void OnDisconnected(ProudSession session)
    {
      ((ChatSession)session).GameSession?.Dispose();
      ((ChatSession)session).GameSession = null;
      base.OnDisconnected(session);
    }

    protected override void OnError(ErrorEventArgs e)
    {
      var peer = (ChatSession)e.Session;
      var sink = Logger;
      if (e.Session != null)
        sink = sink.ForAccount((ChatSession)e.Session);

      var detail = e.Exception.ToString();
      if (detail.Contains("opcode") || detail.Contains("Bad format in"))
      {
        sink.Warning(e.Exception.InnerException.Message);
        if (peer != null && peer.GameSession != null)
          peer.GameSession.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
      }
      else
      {
        sink.Error(e.Exception, "Chat pipeline aborted on an uncategorized fault");
      }

      base.OnError(e);
    }

        #endregion
    }
}

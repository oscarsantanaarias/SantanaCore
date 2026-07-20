using System;
using System.Threading.Tasks;
using SantanaLib;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using Santana.Network.Message.Game;
using ProudNetSrc.Handlers;

namespace Santana.Network.Services
{
  internal class AdminService : ProudMessageHandler
  {
    [MessageHandler(typeof(AdminShowWindowReqMessage))]
    public void ShowWindowHandler(GameSession session)
    {
      var canOpenPanel = session.Player.Account.SecurityLevel <= SecurityLevel.Tester;
      session.SendAsync(new AdminShowWindowAckMessage(canOpenPanel));
    }

    [MessageHandler(typeof(AdminActionReqMessage))]
    public async Task AdminActionHandler(GameServer server, GameSession session, AdminActionReqMessage message)
    {
      var typedCommand = message.Command.GetArgs();

      var caller = session.Player;
      if (caller == null)
        return;

      var wasHandled = await server.CommandManager.Execute(caller, typedCommand);
      if (wasHandled)
        return;

      caller.Channel?.SendMessage(caller, "System", "Command is not implemented." + message, NewChatType.All);
      caller.SendConsoleMessage(S4Color.Red + "Command is not implemented.");
    }
  }
}

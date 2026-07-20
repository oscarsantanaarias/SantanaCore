using System;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using Santana.Network.Message.Game;
using Santana.Network.Message.GameRule;
using Santana.Network.Message.Relay;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;

namespace Santana.Network.Services
{
    internal class UnusedService : ProudMessageHandler
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(UnusedService));

        [MessageHandler(typeof(GameAvatarDurabilityDecreaseReqMessage))]
        public void GameAvatarDurabilityDecreaseReq(GameSession session, GameAvatarDurabilityDecreaseReqMessage message)
        {
        }

        [MessageHandler(typeof(TaskNotifyReqMessage))]
        public void TaskNotifyReq(GameSession session, TaskNotifyReqMessage message)
        {
            session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }

        [MessageHandler(typeof(TaskReguestReqMessage))]
        public void TaskReguestReq(GameSession session, TaskReguestReqMessage message)
        {
            session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
        }
    }
}

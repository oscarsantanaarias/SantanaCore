using System.Threading;
using SantanaLib.Threading.Tasks;

namespace Santana.Network
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using SantanaLib.Collections.Generic;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Sockets;
    using Message.Game;
    using ProudNetSrc;
    using ProudNetSrc.Handlers;
    using Serilog;
    using Serilog.Core;

    internal class MessageHandler<TSession> : ProudMessageHandler
        where TSession : ProudSession
    {
        public static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName,
            nameof(MessageHandler<TSession>));

        private readonly IDictionary<Type, List<Predicate<TSession>>> _sessionRules =
            new Dictionary<Type, List<Predicate<TSession>>>();

        private readonly IList<IMessageHandler> _handlerChain = new List<IMessageHandler>();

        public override async Task<bool> OnMessageReceived(IChannelHandlerContext context, object message)
        {
            if (message == null)
            {
                Logger.Warning("Empty payload arrived on the channel owned by {remoteAddress}; nothing to dispatch", context?.Channel?.RemoteAddress);
                return false;
            }

            if (!GetParameter(context, message, out TSession session))
                throw new Exception("Unable to retrieve session");

            var innerMessage = UnwrapMessage(message);
            if (!ValidateIncomingMessage(session, innerMessage))
                return false;

            _sessionRules.TryGetValue(innerMessage.GetType(), out var rules);

            if (rules != null && rules.Any(rule => !rule(session)))
            {
                Logger.Debug("Gate rules refused {messageName} raised by {remoteAddress}; discarded before dispatch", innerMessage.GetType().Name,
                    ((ISocketChannel)context.Channel).RemoteAddress);
                return false;
            }

            try
            {
                using (var timeoutSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    await HandleMessage(context, message).WaitAsync(timeoutSource.Token);

                if (message.GetType().Name == "RecvContext")
                {
                    var _ = (RecvContext)message;
                }
            }
            catch (TaskCanceledException)
            {
                await OnUnhandledMessage(context, message, true);
            }
            catch (Exception e)
            {
                context.FireExceptionCaught(e);
            }

            return true;
        }

        private async Task OnUnhandledMessage(IChannelHandlerContext context, object message, bool timeOut = false)
        {
            if (!GetParameter(context, message, out TSession session))
                throw new Exception("Unable to retrieve session");

            if (message.GetType() == null)
                return;

            var reportedName = message.GetType().Name;

            if (message.GetType().Name == "RecvContext")
            {
                var wrapper = (RecvContext)message;
                reportedName = wrapper.Message.GetType().Name;
            }

            if (session.GetType() == typeof(GameSession))
            {
                var typedSession = (GameSession)(object)session;
                Logger.ForAccount(typedSession).Error("No route registered for <{messageName}>; request expired without a handler, aborted: {timeout}", reportedName, timeOut);
            }
            else if (session.GetType() == typeof(ChatSession))
            {
                var typedSession = (ChatSession)(object)session;
                Logger.ForAccount(typedSession).Error("No route registered for <{messageName}>; request expired without a handler, aborted: {timeout}", reportedName, timeOut);
            }
            else
            {
                Logger.Error("Unhandled message {messageName}", reportedName);
            }

            if (session.GetType() == typeof(GameSession))
            {
                var gameSession = (GameSession)(object)session;
                if (gameSession.Player?.Room == null || !(gameSession.Player?.RoomInfo?.HasLoaded ?? true))
                {
                    await gameSession.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
                }
            }
        }

        private async Task<bool> HandleMessage(IChannelHandlerContext context, object message)
        {
            if (!GetParameter(context, message, out TSession session))
                throw new Exception("Unable to retrieve session");

            foreach (var handler in _handlerChain)
            {
                try
                {
                    if (await handler.OnMessageReceived(context, message))
                        return true;
                }
                catch (Exception ex)
                {
                    context.FireExceptionCaught(ex);
                }
            }

            await OnUnhandledMessage(context, message);
            return false;
        }

        public MessageHandler<TSession> AddHandler(IMessageHandler handler)
        {
            _handlerChain.Add(handler);
            return this;
        }

        public MessageHandler<TSession> RegisterRule<T>(params Predicate<TSession>[] predicates)
        {
            if (predicates == null)
                throw new ArgumentNullException(nameof(predicates));

            _sessionRules.AddOrUpdate(typeof(T),
                new List<Predicate<TSession>>(predicates),
                (key, existing) =>
                {
                    existing.AddRange(predicates);
                    return existing;
                });
            return this;
        }

        public MessageHandler<TSession> RegisterRule<T>(Predicate<TSession> predicate)
        {
            _sessionRules.AddOrUpdate(typeof(T),
                new List<Predicate<TSession>> { predicate },
                (key, existing) =>
                {
                    existing.Add(predicate);
                    return existing;
                });
            return this;
        }

        private static object UnwrapMessage(object message)
        {
            return message is RecvContext wrapper ? wrapper.Message : message;
        }

        private static bool ValidateIncomingMessage(TSession session, object message)
        {
            if (message == null)
            {
                LogRejected(session, "unwrapped payload came back empty", "unknown");
                return false;
            }

            var messageType = message.GetType();
            foreach (var property in messageType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead)
                    continue;

                object fieldValue;
                try
                {
                    fieldValue = property.GetValue(message);
                }
                catch (Exception ex)
                {
                    LogRejected(session, $"member {property.Name} threw while being pulled off the DTO: {ex.Message}", messageType.Name);
                    return false;
                }

                if (property.PropertyType == typeof(string))
                {
                    if (!ValidateStringField(session, messageType.Name, property.Name, (string)fieldValue))
                        return false;
                    continue;
                }

                if (property.PropertyType.IsArray && fieldValue is Array asArray)
                {
                    if (!ValidateArrayField(session, messageType.Name, property.Name, asArray))
                        return false;
                    continue;
                }

                if (property.PropertyType.IsEnum && fieldValue != null &&
                    !Attribute.IsDefined(property.PropertyType, typeof(FlagsAttribute)) &&
                    !Enum.IsDefined(property.PropertyType, fieldValue))
                {
                    LogRejected(session, $"member {property.Name} carries {fieldValue}, which is outside its declared enum set", messageType.Name);
                    return false;
                }
            }

            return true;
        }

        private static bool ValidateStringField(TSession session, string messageName, string fieldName, string value)
        {
            if (value == null)
                return true;

            var cap = GetMaxStringLength(fieldName);
            if (value.Length > cap)
            {
                LogRejected(session, $"text member {fieldName} carries {value.Length} chars, above the {cap} ceiling", messageName);
                return false;
            }

            if (value.Any(ch => char.IsControl(ch) && ch != '\r' && ch != '\n' && ch != '\t'))
            {
                LogRejected(session, $"text member {fieldName} embeds a non-printable code point", messageName);
                return false;
            }

            return true;
        }

        private static int GetMaxStringLength(string fieldName)
        {
            var lowered = fieldName.ToLowerInvariant();
            if (lowered.Contains("username") || lowered.Contains("nickname"))
                return 16;
            if (lowered.Contains("password"))
                return 32;
            if (lowered.Contains("token") || lowered.Contains("hwid"))
                return 128;
            if (lowered.Contains("name") || lowered.Contains("title"))
                return 64;
            if (lowered.Contains("chat") || lowered.Contains("message") || lowered.Contains("content"))
                return 512;
            return 1024;
        }

        private static bool ValidateArrayField(TSession session, string messageName, string fieldName, Array array)
        {
            var cap = array.GetType().GetElementType() == typeof(byte) ? 65536 : 512;
            if (array.Length <= cap)
                return true;

            LogRejected(session, $"sequence member {fieldName} holds {array.Length} entries, above the {cap} ceiling", messageName);
            return false;
        }

        private static void LogRejected(TSession session, string reason, string messageName)
        {
            Console.WriteLine($"[GameServer] {messageName} failed inbound screening, cause: {reason}");
            if (session is GameSession gameSession)
                Logger.ForAccount(gameSession).Warning("Inbound {messageName} did not pass screening, cause: {reason}", messageName, reason);
            else
                Logger.Warning("Inbound {messageName} did not pass screening, cause: {reason}", messageName, reason);
        }
    }
}

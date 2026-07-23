using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using ProudNetSrc;
using ProudNetSrc.Handlers;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using SantanaLib.Threading.Tasks;
using Serilog;
using Serilog.Core;

namespace Santana.Relay
{
    internal class RelayMessageRouter<TSession> : ProudMessageHandler
        where TSession : ProudSession
    {
        private static readonly ILogger Log_ =
            Log.ForContext(Constants.SourceContextPropertyName, "Relay");

        private readonly Dictionary<Type, List<Predicate<TSession>>> _rules =
            new Dictionary<Type, List<Predicate<TSession>>>();

        private readonly List<IMessageHandler> _chain = new List<IMessageHandler>();

        public RelayMessageRouter<TSession> AddHandler(IMessageHandler handler)
        {
            _chain.Add(handler);
            return this;
        }

        public RelayMessageRouter<TSession> RegisterRule<T>(Predicate<TSession> predicate)
        {
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            if (_rules.TryGetValue(typeof(T), out var existing))
                existing.Add(predicate);
            else
                _rules[typeof(T)] = new List<Predicate<TSession>> { predicate };

            return this;
        }

        public override async Task<bool> OnMessageReceived(IChannelHandlerContext context, object message)
        {
            if (message == null)
                return false;

            if (!GetParameter(context, message, out TSession session))
                throw new Exception("No pude sacar la sesion del contexto");

            var inner = message is RecvContext wrapper ? wrapper.Message : message;

            try
            {
                var _relayData = inner.GetType().GetProperty("Data")?.GetValue(inner) as byte[];
                if (_relayData != null)
                    Log_.Information("[RELAY-IN] {Msg} len={L} head={H}", inner.GetType().Name, _relayData.Length,
                        BitConverter.ToString(_relayData, 0, Math.Min(20, _relayData.Length)));
                else
                    Log_.Information("[RELAY-IN] {Msg}", inner.GetType().Name);
            }
            catch { Log_.Information("[RELAY-IN] {Msg}", inner.GetType().Name); }

            if (_rules.TryGetValue(inner.GetType(), out var rules) && rules.Any(rule => !rule(session)))
            {
                Log_.Debug("Gate rules rejected {msg} originating from {ep}", inner.GetType().Name,
                    ((ISocketChannel)context.Channel).RemoteAddress);
                return false;
            }

            try
            {
                using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
                    await Dispatch(context, message).WaitAsync(timeout.Token);
            }
            catch (TaskCanceledException)
            {
                Log_.Error("Processing of {msg} blew past its time budget and was abandoned", inner.GetType().Name);
            }
            catch (Exception ex)
            {
                context.FireExceptionCaught(ex);
            }

            return true;
        }

        private async Task<bool> Dispatch(IChannelHandlerContext context, object message)
        {
            foreach (var handler in _chain)
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

            var inner = message is RecvContext wrapper ? wrapper.Message : message;
            Log_.Warning("No route in the chain claimed {msg}", inner.GetType().Name);
            return false;
        }
    }
}

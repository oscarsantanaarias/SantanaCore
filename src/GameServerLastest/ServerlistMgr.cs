using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;
using AuthServer.ServiceModel;
using SantanaLib.DotNetty.SimpleRmi;
using SantanaLib.Threading;
using SantanaLib.Threading.Tasks;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using ExpressMapper.Extensions;
using Santana.Network;
using Serilog;
using Serilog.Core;

namespace Santana
{
    internal class ServerlistMgr : IDisposable
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ServerlistMgr));

        private readonly Bootstrap _clientBootstrap;
        private readonly IEventLoopGroup _loopGroup;
        private readonly ILoop _heartbeat;
        private IChannel _link;
        private bool _isRegistered;
        private bool _shuttingDown;
        private TimeSpan _sinceRegister = TimeSpan.Zero;

        public ServerlistMgr()
        {
            var pipe = new Handler();
            pipe.Connected += Client_Connected;
            pipe.Disconnected += Client_Disconnected;

            _loopGroup = new MultithreadEventLoopGroup(1);
            _clientBootstrap = new Bootstrap()
                .Group(_loopGroup)
                .Channel<TcpSocketChannel>()
                .Handler(new ActionChannelInitializer<IChannel>(ch =>
                {
                    ch.Pipeline.AddLast(new SimpleRmiHandler())
                        .AddLast(pipe);
                }));
            _heartbeat = new TaskLoop(TimeSpan.FromSeconds(5), Worker);
        }

        public void Dispose()
        {
            _heartbeat.Stop();
            _shuttingDown = true;
            try
            {
                if (_link != null && _link.Active && _isRegistered)
                    _link.GetProxy<IServerlistService>().Remove((byte)Config.Instance.Id);
            }
            catch
            {
            }

            _link.CloseAsync().WaitEx();
            _loopGroup.ShutdownGracefullyAsync().WaitEx();
        }

        public void Start()
        {
            _shuttingDown = false;
            _isRegistered = false;
            _heartbeat.Start();
        }

        private async Task Worker(TimeSpan diff)
        {
            if (diff == null)
                return;

            _sinceRegister += diff;
            try
            {
                if (_link == null || !_link.Active)
                {
                    if (!await Connect().ConfigureAwait(false))
                        return;
                }

                if (!_isRegistered)
                {
                    await Register().ConfigureAwait(false);
                    return;
                }

                var pushed = await _link.GetProxy<IServerlistService>()
                    .Update(GameServer.Instance.Map<GameServer, ServerInfoDto>())
                    .ConfigureAwait(false);

                if (_sinceRegister > Config.Instance.AuthAPI.UpdateInterval && !pushed)
                {
                    await Register().ConfigureAwait(false);
                    _sinceRegister = TimeSpan.Zero;
                }
            }
            catch (Exception ex)
            {
                var root = ex.GetBaseException();
                if (root is OperationCanceledException || root is ClosedChannelException ||
                    root is ConnectException)
                {
                    _isRegistered = false;
                    _log.Warning("Login backend is out of reach, scheduling another attempt ({Reason})", root.GetType().Name);
                }
                else
                {
                    _log.Error(ex, "Server-list heartbeat aborted on an unexpected fault");
                }
            }
        }

        private void Client_Connected(object sender, EventArgs e)
        {
            _log.Information("Link with the login backend is up");
        }

        private void Client_Disconnected(object sender, EventArgs e)
        {
            _isRegistered = false;
            if (_shuttingDown)
                return;

            _log.Warning("Link with the login backend dropped unexpectedly");
        }

private async ValueTask<bool> Connect()
{
    var target = Config.Instance.AuthAPI.EndPoint;
            try
            {
                _link = await _clientBootstrap.ConnectAsync(target)
                  .ConfigureAwait(false);
            }
            catch (AggregateException ex)
            {
                var root = ex.GetBaseException();
                if (root is ConnectException)
                {
                    _log.Error("No route to the login backend listening at {endpoint}", target);
                    return false;
                }

                _log.Error(root, "No route to the login backend listening at {endpoint}", target);
            }
            catch (ConnectException ex)
            {
                _log.Error("No route to the login backend listening at {endpoint}", target);
                return false;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "No route to the login backend listening at {endpoint}", target);
                return false;
            }

            return true;
        }

        private async ValueTask<bool> Register()
        {
            var info = GameServer.Instance.Map<GameServer, ServerInfoDto>();
            var outcome = await _link.GetProxy<IServerlistService>()
                .Register(info);

            switch (outcome)
            {
                case RegisterResult.OK:
                    _isRegistered = true;
                    return true;

                case RegisterResult.AlreadyExists:
                    _log.Warning("Announce rejected - slot {id} is taken by another node; check for a duplicated Id in the config",
                        Config.Instance.Id);
                    break;
                case RegisterResult.WrongKey:
                    _log.Warning(
                        "Announce rejected - the login backend refused credential {key}; the two configs are out of step",
                        Config.Instance.AuthAPI.ApiKey);
                    break;
            }

            return false;
        }

        private class Handler : ChannelHandlerAdapter
        {
            public override bool IsSharable => true;
            public event EventHandler Connected;
            public event EventHandler Disconnected;

            public override void ChannelActive(IChannelHandlerContext context)
            {
                Connected?.Invoke(this, EventArgs.Empty);
                base.ChannelActive(context);
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                Disconnected?.Invoke(this, EventArgs.Empty);
                base.ChannelInactive(context);
            }
        }
    }
}

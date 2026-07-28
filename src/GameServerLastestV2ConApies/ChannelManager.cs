
using System.Diagnostics;
using System.Threading.Tasks;
using Santana.Network;

namespace Santana
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using Santana;
    using Santana.Database.Game;

    internal class ChannelManager : IReadOnlyCollection<Channel>
    {
        private readonly ConcurrentDictionary<uint, Channel> _registry = new ConcurrentDictionary<uint, Channel>();

        public ChannelManager(IEnumerable<ChannelDto> channelInfos)
        {
            _registry.TryAdd(0,
                new Channel()
                {
                    Name = "basechannel",
                    PlayerLimit = Config.Instance.PlayerLimit,
                    MaxLevel = 255,
                    MinLevel = 0
                });

            foreach (var row in channelInfos.Where(x => x.Id > 0))
            {
                var entry = new Channel
                {
                    Id = row.Id,
                    Name = row.Name,
                    Description = row.Description,
                    PlayerLimit = row.PlayerLimit,
                    MinLevel = row.MinLevel,
                    MaxLevel = row.MaxLevel,
                    Color = Color.FromArgb((int)row.Color),
                };
                entry.Color = Color.FromArgb(entry.Color.R, entry.Color.G, entry.Color.B);

                entry.PlayerJoined += (s, e) => OnPlayerJoined(e);
                entry.PlayerLeft += (s, e) => OnPlayerLeft(e);
                _registry.TryAdd((uint)row.Id, entry);
            }
        }

        public Channel this[uint id] => GetChannel(id);

        public Channel GetChannel(uint id)
        {
            _registry.TryGetValue(id, out var found);
            return found;
        }

        public void Update(TimeSpan delta)
        {
            Parallel.ForEach(_registry.Values, (channel) => channel?.Update(delta));
        }

        #region Events

        public event EventHandler<ChannelPlayerJoinedEventArgs> PlayerJoined;
        public event EventHandler<ChannelPlayerLeftEventArgs> PlayerLeft;

        protected virtual void OnPlayerJoined(ChannelPlayerJoinedEventArgs e)
        {
            PlayerJoined?.Invoke(this, e);
        }

        protected virtual void OnPlayerLeft(ChannelPlayerLeftEventArgs e)
        {
            PlayerLeft?.Invoke(this, e);
        }

        #endregion

        #region IReadOnlyCollection

        public int Count => _registry.Count;

        public IEnumerator<Channel> GetEnumerator()
        {
            return _registry.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion
    }
}

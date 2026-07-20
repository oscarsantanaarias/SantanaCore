using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Collections.Generic;
using SantanaLib.Threading.Tasks;

namespace Santana
{
    internal class PlayerManager : IReadOnlyCollection<Player>
    {
        private readonly IDictionary<ulong, Player> _sessionsById = new ConcurrentDictionary<ulong, Player>();
        internal readonly AsyncLock _sync = new AsyncLock();

        public Player this[ulong id] => Get(id);

        public int Count => _sessionsById.Count;

        public IEnumerator<Player> GetEnumerator()
        {
            return _sessionsById.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Player Get(ulong id)
        {
            _sessionsById.TryGetValue(id, out var found);
            return found;
        }

        public Player Get(string nick)
        {
            foreach (var session in _sessionsById.Values)
            {
                var currentNick = session.Account.Nickname;
                if (currentNick == null)
                    continue;

                if (currentNick.ToLower().Equals(nick.ToLower(), StringComparison.InvariantCultureIgnoreCase))
                    return session;
            }

            return null;
        }

        public void Add(Player plr)
        {
            var accountId = plr.Account.Id;
            if (!CollectionExtensions.TryAdd(_sessionsById, accountId, plr))
                Console.WriteLine("Registration skipped, account " + accountId + " is already tracked in this channel");
        }

        public void Remove(Player plr)
        {
            if (plr?.Account == null)
                return;

            _sessionsById.TryRemove(plr.Account.Id, out _);
        }

        public bool Contains(Player plr)
        {
            return Contains(plr.Account.Id);
        }

        public bool Contains(ulong id)
        {
            return _sessionsById.ContainsKey(id);
        }
    }
}

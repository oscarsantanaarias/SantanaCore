using SantanaLib.Collections.Concurrent;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace Santana
{
    internal class NFriendManager : IReadOnlyCollection<NFriend>
    {
        private readonly ConcurrentDictionary<ulong, NFriend> _entries = new ConcurrentDictionary<ulong, NFriend>();

        public NFriendManager(Player plr, PlayerDto dto)
        {
            Player = plr;

            foreach (var row in dto.Friends)
            {
                var entry = new NFriend(row);
                _entries.TryAdd(entry.DenyId, entry);
            }
        }

        public Player Player { get; }
        public NFriend this[ulong accountId] => CollectionExtensions.GetValueOrDefault(_entries, accountId);
        public int Count => _entries.Count;

        public IEnumerator<NFriend> GetEnumerator()
        {
            return _entries.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public NFriend Add(Player plr)
        {
            var entry = new NFriend(plr.Account.Id, plr.Account.Nickname);
            if (!_entries.TryAdd(entry.DenyId, entry))
                throw new ArgumentException("Player is already ignored", nameof(plr));
            return entry;
        }

        public bool Contains(ulong accountId)
        {
            return _entries.ContainsKey(accountId);
        }
    }

    internal class NFriend
    {
        internal NFriend(PlayerFriendDto dto)
        {
            ExistsInDatabase = true;
            Id = dto.Id;
            DenyId = (ulong)dto.FriendId;

            var online = GameServer.Instance.PlayerManager[DenyId];
            Nickname = online?.Account.Nickname;
            if (Nickname == null)
            {
                using (var db = AuthDatabase.Open())
                {
                    Nickname = db.Get(new AccountDto { Id = (int)DenyId })?.Nickname ?? "<Player not found>";
                }
            }
        }

        internal NFriend(ulong accountId, string nickname)
        {
            Id = DenyIdGenerator.GetNextId();
            DenyId = accountId;
            Nickname = nickname;
        }

        internal bool ExistsInDatabase { get; set; }

        public int Id { get; }
        public ulong DenyId { get; }
        public string Nickname { get; internal set; }
    }
}

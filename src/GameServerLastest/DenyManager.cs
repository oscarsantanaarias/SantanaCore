using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using SantanaLib.Collections.Concurrent;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network;

namespace Santana
{
    internal class DenyManager : IReadOnlyCollection<Deny>
    {
        private readonly ConcurrentDictionary<ulong, Deny> _ignoreList = new ConcurrentDictionary<ulong, Deny>();
        private readonly ConcurrentStack<Deny> _pendingDeletions = new ConcurrentStack<Deny>();

        public DenyManager(Player plr, PlayerDto dto)
        {
            Player = plr;

            foreach (var storedIgnore in dto.Ignores)
            {
                var entry = new Deny(storedIgnore);
                _ignoreList.TryAdd(entry.DenyId, entry);
            }
        }

        public Player Player { get; }
        public Deny this[ulong accountId] => CollectionExtensions.GetValueOrDefault(_ignoreList, accountId);
        public int Count => _ignoreList.Count;

        public IEnumerator<Deny> GetEnumerator()
        {
            return _ignoreList.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public Deny Add(Player plr)
        {
            var entry = new Deny(plr.Account.Id, plr.Account.Nickname);
            if (!_ignoreList.TryAdd(entry.DenyId, entry))
                throw new ArgumentException("Player is already ignored", nameof(plr));
            return entry;
        }

        public bool Remove(Deny deny)
        {
            return Remove(deny.DenyId);
        }

        public bool Remove(ulong accountId)
        {
            var entry = this[accountId];
            if (entry == null)
                return false;

            _ignoreList.Remove(accountId);
            if (entry.ExistsInDatabase)
                _pendingDeletions.Push(entry);
            return true;
        }

        internal void Save(IDbConnection db)
        {
            if (!_pendingDeletions.IsEmpty)
            {
                var deleteIds = new StringBuilder();
                var isFirst = true;
                Deny dropped;
                while (_pendingDeletions.TryPop(out dropped))
                {
                    if (isFirst)
                        isFirst = false;
                    else
                        deleteIds.Append(',');
                    deleteIds.Append(dropped.Id);
                }

                DbUtil.BulkDelete<PlayerDenyDto>(db, statement => statement
                    .Where($"{nameof(PlayerDenyDto.Id):C} IN ({deleteIds})"));
            }

            foreach (var entry in _ignoreList.Values.Where(entry => !entry.ExistsInDatabase))
            {
                DbUtil.Insert(db, new PlayerDenyDto
                {
                    Id = entry.Id,
                    PlayerId = (int)Player.Account.Id,
                    DenyPlayerId = (int)entry.DenyId
                });
                entry.ExistsInDatabase = true;
            }
        }

        public bool Contains(ulong accountId)
        {
            return _ignoreList.ContainsKey(accountId);
        }
    }

    internal class Deny
    {
        internal Deny(PlayerDenyDto dto)
        {
            ExistsInDatabase = true;
            Id = dto.Id;
            DenyId = (ulong)dto.DenyPlayerId;
            PlayerId = (ulong)dto.PlayerId;
            Nickname = GameServer.Instance.PlayerManager[DenyId]?.Account.Nickname;
            if (Nickname == null)
                using (var db = AuthDatabase.Open())
                {
                    Nickname = DbUtil.Get(db, new AccountDto { Id = (int)DenyId })?.Nickname ?? "<Player not found>";
                }
        }

        internal Deny(ulong accountId, string nickname)
        {
            Id = DenyIdGenerator.GetNextId();
            DenyId = accountId;
            Nickname = nickname;
        }

        internal bool ExistsInDatabase { get; set; }

        public int Id { get; }
        public ulong DenyId { get; }
        public ulong PlayerId { get; }
        public string Nickname { get; internal set; }
    }
}

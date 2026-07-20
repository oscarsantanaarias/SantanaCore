namespace Santana
{
    using System;
    using System.Collections;
    using System.Data;
    using System.Linq;
    using System.Text;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using SantanaLib.Collections.Concurrent;
    using Dapper.FastCrud;
    using Database.Auth;
    using Database.Game;
    using Network;
    using Santana.Network.Data.Chat;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Club;
    using Santana.Network.Message.Game;

    internal class FriendManager : IReadOnlyCollection<Friend>
    {
        internal readonly ConcurrentDictionary<ulong, Friend> _friends = new ConcurrentDictionary<ulong, Friend>();

        public FriendManager(Player plr, PlayerDto dto)
        {
            Player = plr;

            foreach (var friendDto in dto.Friends)
            {
                var friend = new Friend(friendDto);
                _friends.TryAdd(friend.FriendId, friend);
            }

            using (var db = GameDatabase.Open())
            {
                var friends = DbUtil.Find<PlayerFriendDto>(db, statement => statement
                    .Where($"{nameof(PlayerFriendDto.FriendId):C} = @Id")
                    .WithParameters(new { Player.Account.Id }));

                foreach (var friendDto in dto.Friends)
                {
                    var friend = new Friend(friendDto);
                    _friends.TryAdd(friend.FriendId, friend);
                }

                foreach (var friendDto in friends)
                {
                    var fstate = friendDto.FriendState;
                    friendDto.FriendId = friendDto.PlayerId;
                    friendDto.PlayerId = (int)Player.Account.Id;
                    friendDto.FriendState = friendDto.PlayerState;
                    friendDto.PlayerState = fstate;
                    var friend = new Friend(friendDto);
                    _friends.TryAdd(friend.FriendId, friend);
                }
            }
        }

        public Player Player { get; }
        public Friend this[ulong accountId] => CollectionExtensions.GetValueOrDefault(_friends, accountId);
        public int Count => _friends.Count;

        public IEnumerator<Friend> GetEnumerator()
        {
            return _friends.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool GetValue(ulong plr, out Friend friend)
        {
            if (_friends.TryGetValue(plr, out friend))
            {
                return true;
            }

            return false;
        }

        public Friend AddOrUpdate(ulong friendId, Player plr, FriendState playerState, FriendState friendState)
        {
            using (var db = GameDatabase.Open())
            {
                if (_friends.TryGetValue(friendId, out var friend))
                {
                    var dbFriend = DbUtil.Find<PlayerFriendDto>(db, statement => statement
                        .Where(
                            $"{nameof(PlayerFriendDto.PlayerId):C} = @PlayerId AND {nameof(PlayerFriendDto.FriendId):C} = @FriendId")
                        .WithParameters(new { PlayerId = Player.Account.Id, FriendId = friendId })).FirstOrDefault();

                    if (dbFriend == null)
                    {
                        dbFriend = DbUtil.Find<PlayerFriendDto>(db, statement => statement
                            .Where(
                                $"{nameof(PlayerFriendDto.PlayerId):C} = @PlayerId AND {nameof(PlayerFriendDto.FriendId):C} = @FriendId")
                            .WithParameters(new { PlayerId = friendId, FriendId = Player.Account.Id })).FirstOrDefault();
                    }

                    if (dbFriend == null)
                    {
                        Console.WriteLine("Friend row missing from storage while an in-memory entry exists - update aborted");
                        return null;
                    }

                    dbFriend.PlayerState = (int)playerState;
                    dbFriend.FriendState = (int)friendState;
                    DbUtil.Update(db, dbFriend);
                    friend.PlayerState = playerState;
                    friend.FriendState = friendState;
                    return friend;
                }

                var newDbFriend = new PlayerFriendDto()
                {
                    PlayerId = (int)Player.Account.Id,
                    FriendId = (int)friendId,
                    PlayerState = (int)playerState,
                    FriendState = (int)friendState
                };

                DbUtil.Insert(db, newDbFriend);

                var newFriend = new Friend(Player.Account.Id, friendId, playerState, friendState);
                if (!_friends.TryAdd(newFriend.FriendId, newFriend))
                {
                    Console.WriteLine("Friend list insert skipped - an entry for this account is already present", nameof(Player.Account.Id));
                    return null;
                }

                if (plr != null)
                {
                    var newPlrFriend = new Friend(friendId, Player.Account.Id, friendState, playerState);
                    if (!plr.FriendManager._friends.TryAdd(Player.Account.Id, newPlrFriend))
                    {

                        return null;
                    }
                }

                return newFriend;
            }
        }

        public bool Remove(ulong friendId, Player plr)
        {
            var friend = _friends[friendId];
            if (friend == null)
                return false;

            _friends.Remove(friendId);
            if (plr != null)
                plr.FriendManager._friends.Remove(Player.Account.Id);

            using (var db = GameDatabase.Open())
            {
                var dbFriend = DbUtil.Find<PlayerFriendDto>(db, statement => statement
                    .Where(
                        $"{nameof(PlayerFriendDto.PlayerId):C} = @PlayerId AND {nameof(PlayerFriendDto.FriendId):C} = @FriendId")
                    .WithParameters(new { PlayerId = Player.Account.Id, FriendId = friendId })).FirstOrDefault();

                if (dbFriend == null)
                {
                    dbFriend = DbUtil.Find<PlayerFriendDto>(db, statement => statement
                        .Where(
                            $"{nameof(PlayerFriendDto.PlayerId):C} = @PlayerId AND {nameof(PlayerFriendDto.FriendId):C} = @FriendId")
                        .WithParameters(new { PlayerId = friendId, FriendId = Player.Account.Id })).FirstOrDefault();
                }

                if (dbFriend == null)
                {
                    Console.WriteLine("Friend row missing from storage while an in-memory entry exists - delete aborted");
                    return false;
                }

                DbUtil.Delete(db, dbFriend);
            }

            return true;
        }

        public bool Contains(ulong accountId)
        {
            return _friends.ContainsKey(accountId);
        }

        public void Broadcast(object message)
        {
            foreach (var member in GameServer.Instance.PlayerManager.Where(x => _friends.ContainsKey(x.Account.Id)))
                member.SendAsync(message);
        }
    }

    internal class Friend
    {
        internal Friend(PlayerFriendDto dto)
        {
            AccountId = (ulong)dto.PlayerId;
            FriendId = (ulong)dto.FriendId;
            PlayerState = (FriendState)dto.PlayerState;
            FriendState = (FriendState)dto.FriendState;
        }

        internal Friend(ulong accountId, ulong friendId, FriendState playerState, FriendState friendState)
        {
            AccountId = accountId;
            FriendId = friendId;
            PlayerState = playerState;
            FriendState = friendState;
        }

        public ulong AccountId { get; }
        public ulong FriendId { get; }

        public FriendState PlayerState { get; set; }
        public FriendState FriendState { get; set; }

        public FriendDto GetPlayer()
        {
            using (var db = GameDatabase.Open())
            {
                var accPlr = GameServer.Instance.PlayerManager.Get(AccountId);
                return new FriendDto()
                {
                    AccountId = AccountId,
                    Nickname = accPlr != null
                        ? accPlr.Account.Nickname
                        : DbUtil.Get(db, new AccountDto { Id = (int)AccountId })?.Nickname ?? "",
                    State = (uint)FriendState
                };
            }
        }

        public FriendDto GetFriend()
        {
            using (var db = GameDatabase.Open())
            {
                var accPlr = GameServer.Instance.PlayerManager.Get(FriendId);
                return new FriendDto()
                {
                    AccountId = FriendId,
                    Nickname = accPlr != null
                        ? accPlr.Account.Nickname
                        : DbUtil.Get(db, new AccountDto { Id = (int)FriendId })?.Nickname ?? "",
                    State = (uint)PlayerState
                };
            }
        }
    }
}

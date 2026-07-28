using System.Threading.Tasks;
namespace Santana
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using ExpressMapper.Extensions;
    using Santana.Network;
    using Santana.Network.Data.Chat;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Game;
    using Santana;
    using Santana.Network.Services;
    using Santana.Database.Game;
    using Santana.Network.Message.Club;
    internal class Channel
    {
        private readonly IDictionary<ulong, Player> _players = new ConcurrentDictionary<ulong, Player>();
        public Channel()
        {
            RoomManager = new RoomManager(this);
        }
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int PlayerLimit { get; set; }
        public byte MinLevel { get; set; }
        public byte MaxLevel { get; set; }
        public Color Color { get; set; }
        public IReadOnlyDictionary<ulong, Player> Players => (IReadOnlyDictionary<ulong, Player>)_players;
        public RoomManager RoomManager { get; }
        public void Update(TimeSpan delta)
        {
            RoomManager?.Update(delta);
        }
        public void NewPlayerList(Player plr, int Type)
        {
            string PlayerInfo = "";
            string FriendStats = "";
            string BlockStats = "";
            ClubRank ClanStats;
            if (plr.Channel.Id > 0)
            {
                if (Type == 0)
                {
                    foreach (var xplr in Players.Values.Where(x => x.Room == null))
                    {
                        var IsPlayerFriend = plr.FriendManager[xplr.Account.Id];
                        var IsPlayerBlocked = plr.DenyManager[xplr.Account.Id];
                        if (IsPlayerFriend == null)
                            FriendStats = "Add Friend";
                        else
                            FriendStats = "Remove Friend";
                        if (IsPlayerBlocked == null)
                            BlockStats = "Block Chat";
                        else
                            BlockStats = "Remove Chat Block";
                        if (xplr.Club != null)
                            ClanStats = xplr.Club.GetPlayerRank(xplr.Account.Id);
                        else
                            ClanStats = ClubRank.None;
                        string namecolor = "";
                        if (!string.IsNullOrWhiteSpace(xplr.Account.AccountDto.Color))
                            namecolor = "{" + xplr.Account.AccountDto.Color + "}";
                    }
                }
                else if (Type == 1)
                {
                    if (plr.Channel.Id > 0)
                    {
                        foreach (var xplr in Players.Values.Where(x => x.Room == null))
                        {
                            var IsPlayerFriend = plr.FriendManager[xplr.Account.Id];
                            var IsPlayerBlocked = plr.DenyManager[xplr.Account.Id];
                            if (IsPlayerFriend == null)
                                FriendStats = "Add Friend";
                            else
                                FriendStats = "Remove Friend";
                            if (IsPlayerBlocked == null)
                                BlockStats = "Block Chat";
                            else
                                BlockStats = "Remove Block";
                            if (plr.Club != null)
                                ClanStats = plr.Club?.GetPlayerRank(plr.Account.Id) ?? ClubRank.None;
                            else
                                ClanStats = ClubRank.None;
                            string namecolor = "";
                            if (!string.IsNullOrWhiteSpace(plr.Account.AccountDto.Color))
                                namecolor = "{" + plr.Account.AccountDto.Color + "}";
                            PlayerInfo = $"Info|{plr.Account.Id}|{namecolor + plr.Account.Nickname}|{plr.Level}|{FriendStats}|{BlockStats}|{ClanStats}";
                        }
                    }
                }
            }
        }
        public void Join(Player plr)
        {
            if (plr.Channel != null)
                throw new ChannelException("Player is already inside a channel");
            if (Id > 0 && Players.Count >= PlayerLimit)
                throw new ChannelLimitReachedException();
            if (Id > 0 && (plr.Level < MinLevel || plr.Level > MaxLevel) &&
                plr.Account.SecurityLevel < SecurityLevel.GameSage)
                throw new ChannelLevelLimitException();
            if (CollectionExtensions.TryAdd(_players, plr.Account.Id, plr))
            {
                plr.Channel = this;
                if (Id > 0)
                {
                    plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.ChannelEnter));
                }
                BroadcastExcept(plr, new ChannelEnterPlayerAckMessage(plr.Map<Player, PlayerInfoShortDto>()));
                SendPlayerlist(plr);
                plr.CharacterManager.Boosts.PlayerNameTag();
                NewPlayerList(plr, 1);
                Club.SendAllLivePresenceTo(plr, "CHANNEL.JOIN.SNAPSHOT");
                Club.BroadcastLivePresence(plr, "CHANNEL.JOIN.BROADCAST");
                UnionService.SLogOn(plr);
                try
                {
                    plr.SendAsync(new NoteCountAckMessage((byte)plr.Mailbox.Count(mail => mail.IsNew), 0, 0));
                }
                finally
                {
                    OnPlayerJoined(new ChannelPlayerJoinedEventArgs(this, plr));
                }
            }
        }
        public void SendPlayerlist(Player plr)
        {
            if (plr == null || plr.Channel != this)
                return;
            var visibleplayers = Players.Values.Where(x => x.Room == null).ToList();
            plr.SendAsync(new ChannelPlayerListAckMessage(visibleplayers
                .Select(p => p.Map<Player, PlayerInfoShortDto>()).ToArray()));
            NewPlayerList(plr, 0);
            Broadcast(new Chennel_PlayerNameTagList_AckMessage(Players.Values
                .Select(p => p.Map<Player, PlayerNameTagInfoDto>()).ToArray()));
        }
        public void Leave(Player plr)
        {
            if (plr.Channel != this)
                throw new ChannelException("Player is not in this channel");
            if (CollectionExtensions.Remove(_players, plr.Account.Id, out _))
            {
                plr.Channel = null;
                try
                {
                    if (Id > 0)
                    {
                        foreach (var xplr in Players.Values)
                        {
                            if (xplr == plr)
                                continue;
                        }
                        plr.SendAsync(new ServerResultAckMessage(ServerResult.ChannelLeave));
                        AuthService.LoadPlayerNameTag(plr, false, false);
                    }
                }
                finally
                {
                    Broadcast(new ChannelLeavePlayerAckMessage(plr.Account.Id));
                    OnPlayerLeft(new ChannelPlayerLeftEventArgs(this, plr));
                }
            }
        }
        #region Broadcast
        public void SendMessage(Player plr, string nickname, string message, NewChatType type)
        {
            OnMessage(new ChannelMessageEventArgs(this, plr, message));
            foreach (var p in Players.Values.Where(p => !p.DenyManager.Contains(plr.Account.Id) && p.Room == null))
            {
                p.SendAsync(new MessageChatAckMessage(ChatType.Channel, plr.Account.Id, plr.Account.Nickname, message));
            }
        }
        public void SendChatMessage(Player plr, string message)
        {
            OnMessage(new ChannelMessageEventArgs(this, plr, message));
            foreach (var p in Players.Values.Where(p => !p.DenyManager.Contains(plr.Account.Id) && p.Room == null))
            {
                p.SendAsync(new MessageChatAckMessage(ChatType.Channel, plr.Account.Id, plr.Account.Nickname, message));
            }
        }
        public void BroadcastNotice(string message)
        {
            Broadcast(new NoticeAdminMessageAckMessage(message));
        }
        public void BroadcastCencored(RoomChangeRoomInfoAck2Message message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(plr => plr.Room?.Id == message.Room.RoomId))
                plr.SendAsync(message);
            var cencored = message.Map<RoomChangeRoomInfoAck2Message, RoomChangeRoomInfoAck2Message>();
            cencored.Room.Password =
                !string.IsNullOrWhiteSpace(message.Room.Password) || !string.IsNullOrEmpty(message.Room.Password)
                    ? "..."
                    : "";
            foreach (var plr in Players.Values.Where(plr => plr.Room?.Id != message.Room.RoomId || plr.Room == null))
                plr.SendAsync(cencored);
        }
        public void Broadcast(object message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(plr => plr.Room == null))
                plr.SendAsync(message);
        }
        public void BroadcastExcept(Player blacklisted, object message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(x => x.Room == null && x != blacklisted))
                plr.SendAsync(message);
        }
        public void BroadcastExcept(List<Player> blacklist, object message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(x => x.Room == null && !blacklist.Contains(x)))
                plr.SendAsync(message);
        }
        #endregion
        #region Events
        public event EventHandler<ChannelPlayerJoinedEventArgs> PlayerJoined;
        public event EventHandler<ChannelPlayerLeftEventArgs> PlayerLeft;
        public event EventHandler<ChannelMessageEventArgs> Message;
        protected virtual void OnPlayerJoined(ChannelPlayerJoinedEventArgs e)
        {
            PlayerJoined?.Invoke(this, e);
        }
        protected virtual void OnPlayerLeft(ChannelPlayerLeftEventArgs e)
        {
            PlayerLeft?.Invoke(this, e);
        }
        protected virtual void OnMessage(ChannelMessageEventArgs e)
        {
            Message?.Invoke(this, e);
        }
        #endregion
    }
}

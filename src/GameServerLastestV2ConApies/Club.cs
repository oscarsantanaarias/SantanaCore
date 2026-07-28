using Santana.Network.Data.Chat;
namespace Santana
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Data;
    using System.Linq;
    using System.Threading.Tasks;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using MySqlConnector;
    using Santana.Database.Auth;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Data.Club;
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Club;
    using Santana.Network.Message.Game;
    using Santana.Network.Services;
    using Serilog;
    using Serilog.Core;
    using static System.Collections.Specialized.BitVector32;
    internal class DBClubInfoDto
    {
        public ClubDto ClubDto { get; set; }
        public ClubPlayerInfo[] PlayerDto { get; set; }
    }
    internal class ClubPlayerInfo
    {
        public ulong AccountId { get; set; }
        public ClubState State { get; set; }
        public ClubRank Rank { get; set; }
        public AccountDto Account { get; set; }
    }
    internal class Club
    {
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, "GameClubMgr");
        public Club(ClubDto dto, IEnumerable<ClubPlayerInfo> player)
        {
            Players = new ConcurrentDictionary<ulong, ClubPlayerInfo>(player.ToDictionary(playerinfo =>
                playerinfo.AccountId));
            Id = dto.Id;
            ClanName = dto.Name;
            ClanIcon = dto.Icon;
            Level = dto.Level;
            Exp = dto.Exp;
            ClanRank = dto.Rank;
            ClubPoints = dto.Points;
            ClubWin = dto.Win;
            ClubLoss = dto.Loss;
            Title = dto.Title;
            Message = dto.Message;
            _ = CheckMaster().ContinueWith(task =>
            {
                if (task.Exception != null)
                    Logger.Error(task.Exception, "Leadership verification did not complete for clan {clubId}", Id);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
        public static bool operator !=(Club a, Club b)
        {
            return a?.Id != b?.Id;
        }
        public static bool operator ==(Club a, Club b)
        {
            return a?.Id == b?.Id;
        }
        public ConcurrentDictionary<ulong, ClubPlayerInfo> Players { get; }
        public ClubPlayerInfo this[ulong id] => GetPlayer(id);
        public int Count => Players.Count;
        public uint Id { get; }
        public string ClanIcon { get; set; } = "1-1-1";
        public string ClanName { get; set; } = "ambi";
        public int Level { get; }
        public uint Exp { get; }
        public uint ClubPoints { get; private set; }
        public uint ClubWin { get; private set; }
        public uint ClubLoss { get; private set; }
        public uint ClanRank { get; private set; }
        public string Title { get; }
        public string Message { get; }
        public void ApplyClubWarStats(uint rank, uint points, uint win, uint loss)
        {
            ClanRank = rank;
            ClubPoints = points;
            ClubWin = win;
            ClubLoss = loss;
        }
        public ClubPlayerInfo GetPlayer(ulong id)
        {
            Players.TryGetValue(id, out var returnval);
            return returnval;
        }
        public ClubRank GetPlayerRank(ulong id)
        {
            using (var db = GameDatabase.Open())
            {
                var player = db.Find<ClubPlayerDto>(statement => statement
                     .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @{nameof(id)}")
                     .WithParameters(new { id })).FirstOrDefault();
                return (ClubRank)player.Rank;
            }
        }
        public async Task CheckMaster()
        {
            if (Players.Any(x => x.Value.Rank == ClubRank.Master))
                return;
            var nextMaster = Players.Values.OrderBy(x => x.Rank).FirstOrDefault(x => x.Rank <= ClubRank.Member);
            if (nextMaster != null)
            {
                nextMaster.Rank = ClubRank.Master;
                using (var db = GameDatabase.Open())
                {
                    var nextMasterDto = new ClubPlayerDto
                    {
                        PlayerId = nextMaster.Account.Id,
                        ClubId = Id,
                        Rank = (byte)ClubRank.Master,
                        State = (int)ClubState.Joined
                    };
                    await DbUtil.UpdateAsync(db, nextMasterDto);
                }
                Logger.Information("Clan {0} had no leader; ownership handed over to {1}", ClanName,
                    nextMaster.Account.Nickname);
            }
        }
        public async Task<bool> ChangeStaffStatus(string nickname, bool isStaff)
        {
            AccountDto account;
            using (var db = AuthDatabase.Open())
            {
                account = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = nickname }))
                    ).FirstOrDefault();
                if (account == null)
                    return false;
            }
            return await ChangeStaffStatus((ulong)account.Id, isStaff);
        }
        public async Task<bool> ChangeStaffStatus(ulong target, bool isStaff)
        {
            if (!Players.TryGetValue(target, out var clubPlr))
                return false;
            clubPlr.Rank = isStaff ? ClubRank.Staff : ClubRank.Member;
            var plrDto = new ClubPlayerDto
            {
                PlayerId = clubPlr.Account.Id,
                ClubId = Id,
                Rank = (byte)(isStaff ? ClubRank.Staff : ClubRank.Member),
                State = (int)ClubState.Joined
            };
            using (var db = GameDatabase.Open())
            {
                await DbUtil.UpdateAsync(db, plrDto);
            }
            return true;
        }
        public async Task<bool> ForceChangeMaster(string nickname)
        {
            AccountDto account;
            using (var db = AuthDatabase.Open())
            {
                account = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = nickname }))
                    ).FirstOrDefault();
                if (account == null)
                    return false;
            }
            return await ForceChangeMaster((ulong)account.Id);
        }
        public async Task<bool> ForceChangeMaster(ulong target)
        {
            if (!Players.TryGetValue(target, out var clubPlr))
                return false;
            var clubMaster = Players.FirstOrDefault(x => x.Value.Rank == ClubRank.Master).Value;
            if (clubMaster != null)
            {
                clubMaster.Rank = ClubRank.Member;
                using (var db = GameDatabase.Open())
                {
                    var oldMasterDto = new ClubPlayerDto
                    {
                        PlayerId = clubMaster.Account.Id,
                        ClubId = Id,
                        Rank = (byte)ClubRank.Member,
                        State = (int)ClubState.Joined
                    };
                    await DbUtil.UpdateAsync(db, oldMasterDto);
                }
            }
            clubPlr.Rank = ClubRank.Master;
            var plrDto = new ClubPlayerDto
            {
                PlayerId = clubPlr.Account.Id,
                ClubId = Id,
                Rank = (byte)ClubRank.Master,
                State = (int)ClubState.Joined
            };
            using (var db = GameDatabase.Open())
            {
                await DbUtil.UpdateAsync(db, plrDto);
            }
            return true;
        }
        public async Task<bool> ChangeMaster(Player plr, ulong target)
        {
            if (Players.TryGetValue(plr.Account.Id, out var clubMaster))
            {
                if (Players.TryGetValue(target, out var clubPlr))
                {
                    if (clubMaster.Rank == ClubRank.Master)
                    {
                        clubPlr.Rank = ClubRank.Master;
                        clubMaster.Rank = ClubRank.Member;
                        var plrDto = new ClubPlayerDto
                        {
                            PlayerId = clubPlr.Account.Id,
                            ClubId = Id,
                            Rank = (byte)ClubRank.Master,
                            State = (int)ClubState.Joined
                        };
                        var oldMasterDto = new ClubPlayerDto
                        {
                            PlayerId = clubMaster.Account.Id,
                            ClubId = Id,
                            Rank = (byte)ClubRank.Member,
                            State = (int)ClubState.Joined
                        };
                        using (var db = GameDatabase.Open())
                        {
                            await DbUtil.UpdateAsync(db, plrDto);
                            await DbUtil.UpdateAsync(db, oldMasterDto);
                        }
                        return true;
                    }
                }
            }
            return false;
        }
        public async Task<bool> RemoveKickPlayer(ulong target, bool IsBan)
        {
            using (var db = GameDatabase.Open())
            {
                var account = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                  .Include<BanDto>(join => join.LeftOuterJoin())
                  .Where($"{nameof(AccountDto.Id):C} = @Id")
                  .WithParameters(new { Id = target }))).FirstOrDefault();
                if (account == null)
                    return false;
                var Player = DbUtil.Find<ClubPlayerDto>(db, statement => statement
                  .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @{nameof(target)}")
                  .WithParameters(new { target })).FirstOrDefault();
                DbUtil.Delete(db, Player);
                if (IsBan)
                {
                     var ClanBan = DbUtil.Find<ClanBannedDto>(db, statement => statement
                       .Where($"{nameof(ClanBannedDto.ClubId):C} = @{nameof(Id)} AND {nameof(ClanBannedDto.PlayerId):C} = @{nameof(target)}")
                       .WithParameters(new { ClubId = Id, PlayerId = target })).FirstOrDefault();
                     if (ClanBan == null)
                     {
                    var clanBanned = new ClanBannedDto
                    {
                        ClubId = Id,
                        PlayerId = target
                    };
                    await DbUtil.InsertAsync(db, clanBanned);
                       }
                }
                var player = GameServer.Instance.PlayerManager[target];
                Players.Remove(target, out var _);
                if (player != null)
                {
                    LogOff(player, false);
                    player.Club = null;
                    await player.Session.SendAsync(new ClubMyInfoAckMessage(player.Map<Player, ClubMyInfoDto>()));
                    await player.Session.SendAsync(new ClubUnjoinAck2Message());
                }
            }
            return true;
        }
        public async Task<bool> RemovePlayer(ulong target)
        {
            AccountDto account;
            using (var db = AuthDatabase.Open())
            {
                account = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                     .Include<BanDto>(join => join.LeftOuterJoin())
                     .Where($"{nameof(AccountDto.Id):C} = @Id")
                     .WithParameters(new { Id = target }))).FirstOrDefault();
                if (account == null)
                    return false;
            }
            using (var db = GameDatabase.Open())
            {
                var clubPlrDto = new ClubPlayerDto
                {
                    PlayerId = account.Id,
                    ClubId = Id
                };
                DbUtil.Delete(db, clubPlrDto);
            }
            var player = GameServer.Instance.PlayerManager[target];
            Players.Remove(target, out var _);
            if (player != null)
            {
                LogOff(player, false);
                player.Club = null;
                await player.Session.SendAsync(new ClubMyInfoAckMessage(player.Map<Player, ClubMyInfoDto>()));
            }
            await CheckMaster();
            return true;
        }
        public void ClanInfos(Player plr)
        {
            if (plr?.Club == null || plr.Club.Id <= 0)
                return;
            using (var db = GameDatabase.Open())
            {
                var clubDto = DbUtil.Find<ClubDto>(db, statement => statement
                    .Where($"{nameof(ClubDto.Id):C} = @Id")
                    .WithParameters(new { Id = plr.Club.Id }))
                    .FirstOrDefault();
                if (clubDto == null)
                {
                    return;
                }
                var rank = DbUtil.Find<ClubDto>(db)
                    .OrderByDescending(x => x.Points)
                    .ToList()
                    .FindIndex(c => c.Id == plr.Club.Id) + 1;
                string clanMark = clubDto.Icon;
                if (string.IsNullOrWhiteSpace(clanMark))
                {
                    clanMark = "200-0-200";
                }
                var markParts = clanMark.Split('-');
                string clanImage = markParts.Length > 1 ? markParts[1].Trim() : "0";
                string clanInfo =
                    $"{clubDto.Name}-{clubDto.Level}-logo_{clanImage}-{rank}-{clubDto.Exp}-{clubDto.Points}-{clubDto.Win}/{clubDto.Loss}-{clubDto.Title}-{clubDto.Message}";
                string playerClanInfo = "";
                foreach (var xplr in plr.Club.Players.Values.OrderBy(x => x.Rank))
                {
                    var onlinePlayer = GameServer.Instance.PlayerManager
                        .FirstOrDefault(p => p.Account.Id == xplr.AccountId);
                    var isPlayerFriend = plr.FriendManager[xplr.AccountId];
                    string friendStats = isPlayerFriend == null
                        ? "Add Friend"
                        : "Delete a Friend";
                    string playerStatus = onlinePlayer != null && onlinePlayer.IsLoggedIn()
                        ? "Online"
                        : "Offline";
                    string playerLocation = onlinePlayer?.PlayerLocation() ?? "Offline";
                    var clanPlayerDto = DbUtil.Find<ClubPlayerDto>(db, statement => statement
                        .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { PlayerId = (int)xplr.AccountId }))
                        .FirstOrDefault();
                    var playerDto = DbUtil.Find<PlayerDto>(db, statement => statement
                        .Where($"{nameof(PlayerDto.Id):C} = @Id")
                        .WithParameters(new { Id = (int)xplr.AccountId }))
                        .FirstOrDefault();
                    int level = playerDto?.Level ?? 0;
                    uint points = clanPlayerDto?.Points ?? 0u;
                    uint win = clanPlayerDto?.Win ?? 0u;
                    uint loss = clanPlayerDto?.Loss ?? 0u;
                    string nameColor = "";
                    if (!string.IsNullOrWhiteSpace(xplr.Account?.Color))
                        nameColor = xplr.Account.Color;
                    string nickname = xplr.Account?.Nickname ?? "Unknown";
                    playerClanInfo +=
                        $"Info-{xplr.AccountId}-{nameColor + nickname}-{level}-{playerLocation}-{xplr.Rank}-{points}-{win}/{loss}-{friendStats}-{playerStatus},";
                }
            }
        }
        public async Task<bool> AddPlayer(ulong target)
        {
            AccountDto account;
            using (var db = AuthDatabase.Open())
            {
                 account = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                            .Where($"{nameof(AccountDto.Id):C} = @{nameof(target)}")
                            .WithParameters(new { target }))).FirstOrDefault();
                if (account == null)
                    return false;
            }
           var plrDto = new ClubPlayerDto
            {
                PlayerId = account.Id,
                ClubId = Id,
                Rank = (byte)ClubRank.Member,
                State = (int)ClubState.Joined
            };
            using (var db = GameDatabase.Open())
            {
                var existingClubPlayer = DbUtil.Find<ClubPlayerDto>(db, statement => statement
                    .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @{nameof(target)}")
                    .WithParameters(new { target }))
                    .FirstOrDefault();
                if (existingClubPlayer != null)
                {
                    DeletePendingClubRequests(db, target);
                    return false;
                }
                await DbUtil.InsertAsync(db, plrDto);
                DeletePendingClubRequests(db, target);
            }
            var plrInfo = new ClubPlayerInfo
            {
                Account = account,
                AccountId = (ulong)account.Id,
                State = ClubState.Joined,
                Rank = ClubRank.Member
            };
            Players.TryAdd(target, plrInfo);
            var player = GameServer.Instance.PlayerManager[target];
            if (player != null)
            {
                player.Club = this;
                await player.Session.SendAsync(new ClubMyInfoAckMessage(player.Map<Player, ClubMyInfoDto>()));
                LogOn(player);
            }
            return true;
        }
        private static void DeletePendingClubRequests(IDbConnection db, ulong playerId)
        {
            var requests = DbUtil.Find<ClanRequestDto>(db, statement => statement
                    .Where($"{nameof(ClanRequestDto.PlayerId):C} = @{nameof(playerId)}")
                    .WithParameters(new { playerId }))
                .ToArray();
            foreach (var request in requests)
                DbUtil.Delete(db, request);
        }
        public bool SendInvite(Player sender, Player plr)
        {
            if (plr == null)
                return false;
            if ((plr.Club?.Id ?? 0) > 0)
                return false;
            using (var db = GameDatabase.Open())
            {
                var Club = DbUtil.Find<ClubDto>(db, statement => statement
                     .Where($"{nameof(ClubDto.Id):C} = @{nameof(sender.Club.Id)}")
                     .WithParameters(new { sender.Club.Id })).FirstOrDefault();
                var ClubPlayer = DbUtil.Find<ClubPlayerDto>(db, statement => statement
                     .Where($"{nameof(ClubPlayerDto.ClubId):C} = @{nameof(sender.Club.Id)}")
                     .WithParameters(new { sender.Club.Id }));
                ulong PlrId = plr.Account.Id;
                var existingClubPlayer = DbUtil.Find<ClubPlayerDto>(db, statement => statement
                   .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @{nameof(PlrId)}")
                   .WithParameters(new { PlrId })).FirstOrDefault();
                if (existingClubPlayer != null)
                    return false;
                var clanBanned = DbUtil.Find<ClanBannedDto>(db, statement => statement
                   .Where($"{nameof(ClanBannedDto.ClubId):C} = @{nameof(sender.Club.Id)} AND {nameof(ClanBannedDto.PlayerId)} = @{nameof(PlrId)}")
                   .WithParameters(new { sender.Club.Id, PlrId })).FirstOrDefault();
                if (clanBanned != null)
                {
                    return false;
                }
                if (ClubPlayer.Count() > Club.Level * 12)
                {
                    return false;
                }
            }
            sender?.Mailbox.SendAsync(
               plr.Account.Nickname,
               $"<Note Key =\"3\"Cnt =\"1\"Param1=\"{ClanName}\" />",
               $"<Note Key =\"4\"Srl =\"{Id}\"Cnt =\"2\"Param1=\"{ClanName}\"Param2=\"{sender.Account.Nickname}\" />",
               true);
            return true;
        }
        public static void LogOn(Player plr, bool noRooms = false)
        {
            if (plr.Club?.Id > 0)
            {
                string PlayerInfo = "";
                string FriendStats = "";
                string BlockStats = "";
                ClubRank ClanStats;
                plr.Club?.Broadcast(new ClubSystemMessageMessage(plr.Account.Id, $"<Chat Key =\"1\"Cnt =\"2\"Param1=\"{plr.Account.Nickname}\"Param2=\"1\" />"));
                foreach (var xplr in GameServer.Instance.PlayerManager.Where(x => x.Club == plr.Club).ToArray())
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
                      ClanStats = plr.Club?.GetPlayerRank(plr.Account.Id) ?? ClubRank.None;
                    string namecolor = "";
                    if (!string.IsNullOrWhiteSpace(xplr.Account.AccountDto.Color))
                        namecolor = "{" + xplr.Account.AccountDto.Color + "}";
                    PlayerInfo += $"Info|{xplr.Account.Id}|{namecolor + xplr.Account.Nickname}|{xplr.Level}|{FriendStats}|{BlockStats}|{ClanStats},";
                }
                plr.Club?.Broadcast(new ClubMemberLoginStateAckMessage(1, plr.Account.Id));
                plr.SendAsync(new ClanMemberListAckMessage(GameServer.Instance.PlayerManager.Where(x => x.Club == plr.Club).Select(x => x.Map<Player, PlayerInfoDto>()).ToArray()));
                foreach (var xplr in GameServer.Instance.PlayerManager.Where(x => x.Club == plr.Club).ToArray())
                {
                }
                plr.SendAsync(new ClubNewsRemindMessage(-1, 0));
                if (!noRooms)
                {
                    plr.Room?.Broadcast(new RoomPlayerInfoListForEnterPlayerAckMessage(plr.Room.TeamManager.Players
                        .Select(r => r.Room.GetRoomPlrDto(r)).ToArray()));
                    plr.Room?.Broadcast(new RoomEnterClubInfoAckMessage(plr.Map<Player, PlayerClubInfoDto>()));
                }
            }
        }
        public static void LogOff(Player plr, bool noRooms = false)
        {
            try
            {
                plr.Club?.Broadcast(new ClubSystemMessageMessage(plr.Account.Id, $"<Chat Key =\"1\"Cnt =\"2\"Param1=\"{plr.Account.Nickname}\"Param2=\"2\" />"));
                plr.Club?.Broadcast(new ClubMemberLoginStateAckMessage(0, plr.Account.Id));
                SendOfflinePresence(plr);
                foreach (var xplr in GameServer.Instance.PlayerManager.Where(x => x.Club == plr.Club).ToArray())
                {
                }
                if (!noRooms)
                {
                    plr.Room?.Broadcast(new RoomPlayerInfoListForEnterPlayerAckMessage(plr.Room.TeamManager.Players
                        .Select(r => r.Room.GetRoomPlrDto(r)).ToArray()));
                    plr.Room?.Broadcast(new RoomEnterClubInfoAckMessage(plr.Map<Player, PlayerClubInfoDto>()));
                }
            }
            catch { }
        }
        public static void BroadcastLivePresence(Player target, string source)
        {
            if (target?.Account == null)
                return;
            var viewers = GameServer.Instance.PlayerManager
                .Where(x => x?.ChatSession != null && x.Account.Id != target.Account.Id)
                .ToArray();
            foreach (var viewer in viewers)
                SendLivePresence(viewer, target, source);
        }
        public static void SendAllLivePresenceTo(Player viewer, string source)
        {
            if (viewer?.ChatSession == null)
                return;
            var targets = GameServer.Instance.PlayerManager
                .Where(x => x?.Account != null && x.ChatSession != null && x.Account.Id != viewer.Account.Id)
                .ToArray();
            foreach (var target in targets)
                SendLivePresence(viewer, target, source);
        }
        public static void SendLivePresence(Player viewer, Player target, string source)
        {
            if (viewer?.ChatSession == null || target?.Account == null)
                return;
            var info = target.Map<Player, PlayerInfoDto>();
            ForceLiveLocation(target, info.Location);
            _ = viewer.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(2, target.Account.Id));
            _ = viewer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(info));
            _ = viewer.ChatSession.SendAsync(new PlayerPositionAckMessage(target.Account.Id, info.Location));
        }
        private static void ForceLiveLocation(Player target, PlayerLocationDto location)
        {
            var online = target?.Account != null && GameServer.Instance.PlayerManager.Contains(target.Account.Id);
            if (!online)
            {
                location.ServerGroupId = -1;
                location.ChannelId = -1;
                location.RoomId = -1;
                location.Unk = -1;
                location.GameServerId = -1;
                location.ChatServerId = -1;
                return;
            }
            location.ServerGroupId = Config.Instance.Id;
            location.ChannelId = target.Channel?.Id > 0 ? target.Channel.Id : -1;
            location.RoomId = target.Room?.Id > 0 ? (int)target.Room.Id : -1;
            location.Unk = -2;
            location.GameServerId = Math.Max(2, (int)Config.Instance.Id);
            location.ChatServerId = Math.Max(1, (int)Config.Instance.Id);
        }
        private static void SendOfflinePresence(Player plr)
        {
            var info = plr.Map<Player, PlayerInfoDto>();
            info.Location = new PlayerLocationDto();
            var viewers = GameServer.Instance.PlayerManager
                .Where(x => x?.ChatSession != null && x.Account.Id != plr.Account.Id)
                .ToArray();
            foreach (var viewer in viewers)
            {
                _ = viewer.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(0, plr.Account.Id));
                _ = viewer.ChatSession.SendAsync(new ChatPlayerInfoAckMessage(info));
                _ = viewer.ChatSession.SendAsync(new PlayerPositionAckMessage(plr.Account.Id, info.Location));
            }
        }
        public void Broadcast(object message)
        {
            foreach (var member in GameServer.Instance.PlayerManager.Where(x => x.Club == this))
                member.SendAsync(message);
        }
    }
}

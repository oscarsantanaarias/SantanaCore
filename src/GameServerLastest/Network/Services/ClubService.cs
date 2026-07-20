namespace Santana.Network.Services
{
    using SantanaLib;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using SantanaLib.Threading.Tasks;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using Santana.Database.Auth;
    using Santana.Database.Game;
    using Santana.Network.Data.Chat;
    using Santana.Network.Data.Club;
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Club;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using Santana.Shop;
    using ProudNetSrc.Handlers;
    using Serilog;
    using Serilog.Core;
    using Dapper;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using System.Collections.Concurrent;
    using System.Threading;
    using GameClubJoinReqMessage = Santana.Network.Message.Game.ClubJoinReqMessage;
    using ClubJoinReqClubMessage = Santana.Network.Message.Club.ClubJoinReqMessage;
    internal class ClubService : ProudMessageHandler
    {
        private static readonly ILogger ClubLog =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ClubService));
        private readonly AsyncLock _clubGate = new AsyncLock();
        private static string GetSafeClanIcon(string clanIcon)
        {
            return string.IsNullOrWhiteSpace(clanIcon)
                ? "1-1-1"
                : clanIcon.Trim();
        }
        public static async Task Update(GameSession session = null, bool broadcast = false)
        {
            if (session == null && broadcast == false)
                return;
            var recipients = new List<GameSession>();
            if (broadcast)
                recipients.AddRange(GameServer.Instance.Sessions.Values.Cast<GameSession>());
            else
                recipients.Add(session);
            foreach (var netSession in recipients)
            {
                var member = netSession?.Player;
                if (member == null)
                    continue;
                Club.LogOff(member, true);
                member.Club = GameServer.Instance.ClubManager.GetClubByAccount(member.Account.Id);
                await netSession.SendAsync(new ClubMyInfoAckMessage(member.Map<Player, ClubMyInfoDto>()));
                Club.LogOn(member, true);
                if (member.Room != null)
                {
                    await member.Session.SendAsync(new ClubClubInfoAckMessage(member.Map<Player, ClubInfoDto>()));
                    await member.Session.SendAsync(new ClubClubInfoAck2Message(member.Map<Player, ClubInfoDto2>()));
                }
            }
            foreach (var chan in GameServer.Instance.ChannelManager)
            {
                foreach (var room in chan.RoomManager.Where(x => x.Players.Any(y => y.Value.Club?.Id != 0)))
                {
                    room.Broadcast(new RoomPlayerInfoListForEnterPlayerAckMessage(room.TeamManager.Players
                        .Select(r => r.Room.GetRoomPlrDto(r)).ToArray()));
                    var seenClubs = new List<PlayerClubInfoDto>();
                    foreach (var teamPlayer in room.TeamManager.Players.Where(p => p.Club != null))
                    {
                        if (seenClubs.All(entry => entry.Id != teamPlayer.Club.Id))
                            seenClubs.Add(teamPlayer.Map<Player, PlayerClubInfoDto>());
                    }
                    room.Broadcast(new RoomClubInfoListForEnterPlayerAckMessage(seenClubs.ToArray()));
                }
            }
        }
        [MessageHandler(typeof(ClubClubInfoReqMessage))]
        public void ClubClubInfoReq(GameSession session, ClubClubInfoReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            ClearViewingOtherClub(actor);
            if (actor.Club != null && TryResolveClubSnapshot(out var snap, clubId: actor.Club.Id))
            {
                return;
            }
            session.SendAsync(new ClubClubInfoAckMessage(actor.Map<Player, ClubInfoDto>()));
        }
        [MessageHandler(typeof(ClubClubInfoReq2Message))]
        public void ClubClubInfoReq2(GameSession session, ClubClubInfoReq2Message message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            ClearViewingOtherClub(actor);
            if (actor.Club != null && TryResolveClubSnapshot(out var snap, clubId: actor.Club.Id))
            {
                session.SendAsync(new ClubClubInfoAck2Message(BuildClubInfoDto2FromSnapshot(snap)));
                return;
            }
            session.SendAsync(new ClubClubInfoAck2Message(actor.Map<Player, ClubInfoDto2>()));
        }
        [MessageHandler(typeof(ClubInfoReqMessage))]
        public void ClubInfoReq(GameSession session, ClubInfoReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            ClearViewingOtherClub(actor);
            session.SendAsync(new ClubInfoAckMessage(actor.Map<Player, PlayerClubInfoDto>()));
        }
        [MessageHandler(typeof(ClubJoinWaiterInfoReqMessage))]
        public void ClubJoinWaiterInfoReq(GameSession session, ClubJoinWaiterInfoReqMessage message)
        {
            var actor = session.Player;
            var targetClub = GameServer.Instance.ClubManager[message.ClubId];
            if (actor?.Club == null || targetClub == null || !actor.Club.Players.TryGetValue(actor.Account.Id, out var myMembership))
            {
                session.SendAsync(new ClubJoinWaiterInfoAckMessage());
                return;
            }
            if (myMembership.Rank > ClubRank.Staff)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            var waiterRows = LoadJoinWaiters(targetClub.Id);
            foreach (var row in waiterRows)
            {
            }
            session.SendAsync(new ClubJoinWaiterInfoAckMessage(waiterRows));
        }
        [MessageHandler(typeof(ClubJoinConditionInfoReqMessage))]
        public void ClubJoinConditionInfoReq(GameSession session, ClubJoinConditionInfoReqMessage message)
        {
            var actor = session.Player;
            if (actor == null || message == null || message.ClubId == 0)
            {
                return;
            }
            var targetClub = GameServer.Instance.ClubManager.GetClub(message.ClubId);
            if (targetClub == null)
            {
                session.SendAsync(new ClubJoinConditionInfoAckMessage
                {
                    Unk1 = 0,
                    Unk2 = 0,
                    Unk3 = "",
                    Unk4 = "",
                    Unk5 = "",
                    Unk6 = "",
                    Unk7 = ""
                });
                return;
            }
            using (var gameDb = GameDatabase.Open())
            {
                var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubDto.Id):C} = @Id")
                        .WithParameters(new { Id = targetClub.Id }))
                    .FirstOrDefault();
                var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                        .WithParameters(new { ClubId = targetClub.Id }))
                    .ToList();
                var pendingReq = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId AND {nameof(ClanRequestDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var banRow = DbUtil.Find<ClanBannedDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanBannedDto.ClubId):C} = @ClubId AND {nameof(ClanBannedDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var capacity = Math.Max((clubRow?.Level ?? 1) * 12, 12);
                var headcount = roster.Count;
                session.SendAsync(new ClubJoinConditionInfoAckMessage
                {
                    Unk1 = 0,
                    Unk2 = 0,
                    Unk3 = "",
                    Unk4 = "",
                    Unk5 = "",
                    Unk6 = "",
                    Unk7 = ""
                });
            }
        }
        [MessageHandler(typeof(GameClubJoinReqMessage))]
        public async Task ClubJoinReq(GameSession session, GameClubJoinReqMessage message)
        {
            var actor = session?.Player;
            if (actor == null || message == null || message.ClubId == 0)
            {
                return;
            }
            if (actor.Club != null)
            {
                await actor.SendAsync(new GameClubJoinAckMessage
                {
                    Unk = 1,
                    Message = "Already in club"
                });
                return;
            }
            var targetClub = GameServer.Instance.ClubManager.GetClub(message.ClubId);
            if (targetClub == null)
            {
                await actor.SendAsync(new GameClubJoinAckMessage
                {
                    Unk = 1,
                    Message = "Club not found"
                });
                return;
            }
            var storedNewRequest = false;
            using (var gameDb = GameDatabase.Open())
            {
                var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubDto.Id):C} = @ClubId")
                        .WithParameters(new { ClubId = targetClub.Id }))
                    .FirstOrDefault();
                var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                        .WithParameters(new { ClubId = targetClub.Id }))
                    .ToList();
                var existingReq = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId AND {nameof(ClanRequestDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var banRow = DbUtil.Find<ClanBannedDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanBannedDto.ClubId):C} = @ClubId AND {nameof(ClanBannedDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var capacity = Math.Max((clubRow?.Level ?? 1) * 12, 12);
                if (banRow != null)
                {
                    await actor.SendAsync(new GameClubJoinAckMessage
                    {
                        Unk = 1,
                        Message = "Banned"
                    });
                    return;
                }
                if (clubRow == null || roster.Count >= capacity)
                {
                    await actor.SendAsync(new GameClubJoinAckMessage
                    {
                        Unk = 1,
                        Message = "Club full"
                    });
                    return;
                }
                if (existingReq == null)
                {
                    await DbUtil.InsertAsync(gameDb, new ClanRequestDto
                    {
                        ClubId = targetClub.Id,
                        PlayerId = (ulong)actor.Account.Id
                    });
                    storedNewRequest = true;
                }
                else
                {
                }
            }
            await actor.SendAsync(new GameClubJoinAckMessage
            {
                Unk = 0,
                Message = "OK"
            });
            if (storedNewRequest)
                await NotifyClubJoinWaitersChanged(targetClub.Id, "JoinReq");
        }
        [MessageHandler(typeof(ClubJoinReqClubMessage))]
        public async Task ClubJoinReq4003(GameSession session, ClubJoinReqClubMessage message)
        {
            var actor = session?.Player;
            if (actor == null || message == null || message.ClubId == 0)
            {
                return;
            }
            if (actor.Club != null)
            {
                await actor.SendAsync(new ClubJoinAckMessage { Unk = ClubJoinResult.NotInClan });
                return;
            }
            var targetClub = GameServer.Instance.ClubManager.GetClub(message.ClubId);
            if (targetClub == null)
            {
                await actor.SendAsync(new ClubJoinAckMessage { Unk = ClubJoinResult.Failed });
                return;
            }
            var storedNewRequest = false;
            using (var gameDb = GameDatabase.Open())
            {
                var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubDto.Id):C} = @ClubId")
                        .WithParameters(new { ClubId = targetClub.Id }))
                    .FirstOrDefault();
                var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                        .WithParameters(new { ClubId = targetClub.Id }))
                    .ToList();
                var existingReq = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId AND {nameof(ClanRequestDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var banRow = DbUtil.Find<ClanBannedDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanBannedDto.ClubId):C} = @ClubId AND {nameof(ClanBannedDto.PlayerId):C} = @PlayerId")
                        .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                var capacity = Math.Max((clubRow?.Level ?? 1) * 12, 12);
                if (banRow != null)
                {
                    await actor.SendAsync(new ClubJoinAckMessage { Unk = ClubJoinResult.CantRegister });
                    return;
                }
                if (clubRow == null || roster.Count >= capacity)
                {
                    await actor.SendAsync(new ClubJoinAckMessage { Unk = ClubJoinResult.ClubFull });
                    return;
                }
                if (existingReq == null)
                {
                    await DbUtil.InsertAsync(gameDb, new ClanRequestDto
                    {
                        ClubId = targetClub.Id,
                        PlayerId = (ulong)actor.Account.Id
                    });
                    storedNewRequest = true;
                }
                else
                {
                }
            }
            await actor.SendAsync(new ClubJoinAckMessage { Unk = ClubJoinResult.Registered });
            if (storedNewRequest)
                await NotifyClubJoinWaitersChanged(targetClub.Id, "JoinReq4003");
        }
        [MessageHandler(typeof(ClubStuffListReqMessage))]
        public void ClubStuffListReq(GameSession session, ClubStuffListReqMessage message)
        {
            var actor = session.Player;
            if (actor?.Club != null)
            {
                var staffDtos = new List<ClubMemberDto>();
                using (var gameDb = GameDatabase.Open())
                {
                    foreach (var staffMember in actor.Club.Players.Values.Where(x => x.Rank == ClubRank.Staff))
                    {
                        var entry = staffMember.Map<ClubPlayerInfo, ClubMemberDto>();
                        var accountId = (int)staffMember.AccountId;
                        var clubId = actor.Club.Id;
                        var pointsRow = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                                .Where($"{nameof(ClubPlayerDto.ClubId):C} = @{nameof(clubId)} AND {nameof(ClubPlayerDto.PlayerId):C} = @{nameof(accountId)}")
                                .WithParameters(new { clubId, accountId }))
                            .FirstOrDefault();
                        entry.Unk4 = (int)(pointsRow?.Points ?? 0);
                        entry.Unk5 = (int)(pointsRow?.Points ?? 0);
                        var onlineMember = GameServer.Instance.PlayerManager[staffMember.AccountId];
                        if (onlineMember != null)
                        {
                            entry.Unk1 = onlineMember.Level;
                        }
                        else
                        {
                            var offlineRow = DbUtil.Find<PlayerDto>(gameDb, statement => statement
                                    .Where($"{nameof(PlayerDto.Id):C} = @{nameof(accountId)}")
                                    .WithParameters(new { accountId }))
                                .FirstOrDefault();
                            entry.Unk1 = offlineRow?.Level ?? 0;
                        }
                        staffDtos.Add(entry);
                    }
                }
                session.SendAsync(new ClubStuffListAckMessage(staffDtos.ToArray()));
            }
            else
            {
                session.SendAsync(new ClubStuffListAckMessage());
            }
        }
        [MessageHandler(typeof(ClubStuffListReq2Message))]
        public void ClubStuffListReq2(GameSession session, ClubStuffListReq2Message message)
        {
            var actor = session.Player;
            if (actor?.Club != null)
            {
                var staffDtos = new List<ClubMemberDto2>();
                using (var gameDb = GameDatabase.Open())
                {
                    foreach (var staffMember in actor.Club.Players.Values)
                    {
                        if (staffMember.Rank != ClubRank.Staff)
                            continue;
                        var entry = staffMember.Map<ClubPlayerInfo, ClubMemberDto2>();
                        var accountId = (int)staffMember.AccountId;
                        var clubId = actor.Club.Id;
                        var pointsRow = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                                .Where($"{nameof(ClubPlayerDto.ClubId):C} = @{nameof(clubId)} AND {nameof(ClubPlayerDto.PlayerId):C} = @{nameof(accountId)}")
                                .WithParameters(new { clubId, accountId }))
                            .FirstOrDefault();
                        entry.Unk4 = (int)(pointsRow?.Points ?? 0);
                        var onlineMember = GameServer.Instance.PlayerManager[staffMember.AccountId];
                        if (onlineMember != null)
                        {
                            entry.Unk1 = onlineMember.Level;
                        }
                        else
                        {
                            var offlineRow = DbUtil.Find<PlayerDto>(gameDb, statement => statement
                                    .Where($"{nameof(PlayerDto.Id):C} = @{nameof(accountId)}")
                                    .WithParameters(new { accountId }))
                                .FirstOrDefault();
                            entry.Unk1 = offlineRow?.Level ?? 0;
                        }
                        staffDtos.Add(entry);
                    }
                }
                foreach (var logEntry in staffDtos)
                {
                }
                session.SendAsync(new ClubStuffListAck2Message(staffDtos.ToArray()));
            }
            else
            {
                session.SendAsync(new ClubStuffListAck2Message());
            }
        }
        [MessageHandler(typeof(ClubSearchReqMessage))]
        public void ClubSearchReq(GameSession session, ClubSearchReqMessage message)
        {
            var hits = GameServer.Instance.ClubManager
                .Where(x => x.ClanName.Contains(message.Query, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Map<Club, ClubSearchResultDto>())
                .ToArray();
            session.SendAsync(new ClubSearchAckMessage(hits));
        }
        [MessageHandler(typeof(ClubSearchReq2Message))]
        public void ClubSearchReq2(GameSession session, ClubSearchReq2Message message)
        {
            if (session?.Player == null || message == null)
                return;
            var hits = BuildClubSearchResultsFromDatabase(message.ClubName, message.ClubId);
            if (hits.Length == 0)
            {
                session.SendAsync(new ClubSearchAck2Message());
                return;
            }
            ClubLog.Information(
                "ClubSearch2 query=\"{Query}\" clubId={ClubId} sent={Sent} names=[{Names}]",
                message.ClubName ?? "",
                message.ClubId,
                hits.Length,
                string.Join(", ", hits.Select(entry => entry.Name)));
            session.SendAsync(new ClubSearchAck2Message(hits.Length, hits));
        }
        internal static ClubRankInfoDto[] BuildClubSearchResultsFromDatabase(string clubName, int clubId = 0)
        {
            using (var gameDb = GameDatabase.Open())
            {
                var rankedClubs = DbUtil.Find<ClubDto>(gameDb)
                    .Where(club => club != null && club.Id > 0)
                    .OrderByDescending(club => club.Points)
                    .ThenByDescending(club => club.Win)
                    .ThenBy(club => club.Loss)
                    .ThenBy(club => club.Id)
                    .ToList();
                if (rankedClubs.Count == 0)
                    return Array.Empty<ClubRankInfoDto>();
                IEnumerable<ClubDto> matchSet;
                var needle = clubName?.Trim();
                if (!string.IsNullOrWhiteSpace(needle))
                {
                    var exact = rankedClubs
                        .Where(club => string.Equals(club.Name, needle, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    matchSet = exact.Count > 0
                        ? exact
                        : rankedClubs.Where(club =>
                            club.Name.Contains(needle, StringComparison.OrdinalIgnoreCase));
                }
                else if (clubId > 0)
                {
                    matchSet = rankedClubs.Where(club => club.Id == (uint)clubId);
                }
                else
                {
                    return Array.Empty<ClubRankInfoDto>();
                }
                var picked = matchSet.Take(10).ToList();
                if (picked.Count == 0)
                    return Array.Empty<ClubRankInfoDto>();
                var allMembers = DbUtil.Find<ClubPlayerDto>(gameDb).ToList();
                var countByClub = allMembers
                    .GroupBy(player => player.ClubId)
                    .ToDictionary(group => group.Key, group => (uint)group.Count());
                var masterByClub = allMembers
                    .Where(player => player.Rank == (int)ClubRank.Master)
                    .GroupBy(player => player.ClubId)
                    .ToDictionary(group => group.Key, group => group.First().PlayerId);
                var masterNameByClub = new Dictionary<uint, string>();
                using (var authDb = AuthDatabase.Open())
                {
                    foreach (var pair in masterByClub)
                    {
                        var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                                .Where($"{nameof(AccountDto.Id):C} = @Id")
                                .WithParameters(new { Id = pair.Value }))
                            .FirstOrDefault();
                        if (acct != null)
                            masterNameByClub[pair.Key] = acct.Nickname ?? "";
                    }
                }
                var rankByClub = rankedClubs
                    .Select((club, index) => (club.Id, Rank: (uint)(index + 1)))
                    .ToDictionary(pair => pair.Id, pair => pair.Rank);
                var output = new List<ClubRankInfoDto>();
                foreach (var clubRow in picked)
                {
                    countByClub.TryGetValue(clubRow.Id, out var headcount);
                    masterNameByClub.TryGetValue(clubRow.Id, out var leaderName);
                    leaderName ??= "";
                    rankByClub.TryGetValue(clubRow.Id, out var positionRank);
                    if (positionRank == 0)
                        positionRank = 1;
                    var livingClub = GameServer.Instance.ClubManager.GetClub(clubRow.Id);
                    output.Add(BuildClubRankInfoDto(clubRow, positionRank, headcount, leaderName, livingClub));
                }
                return output.ToArray();
            }
        }
        [MessageHandler(typeof(ClubNameCheckReqMessage))]
        public void ClubNameCheckReq(GameSession session, ClubNameCheckReqMessage message)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            bool asciiOnly = Config.Instance.Game.NickRestrictions.AsciiOnly;
            if (!Namecheck.IsNameValid(message.Name, true))
            {
                session.SendAsync(new NickCheckAckMessage(true));
            }
            else if (GameServer.Instance.ClubManager.Any(c => c.ClanName == message.Name))
            {
                session.SendAsync(new ClubNameCheckAckMessage(2));
            }
            else
            {
                session.SendAsync(new ClubNameCheckAckMessage(0));
            }
        }
        [MessageHandler(typeof(ClubCreateReqMessage))]
        public async Task ClubCreateReq(GameSession session, ClubCreateReqMessage message)
        {
            await ClubCreateReq2(
                session,
                message.Map<ClubCreateReqMessage, ClubCreateReq2Message>()
            );
        }
        [MessageHandler(typeof(ClubCreateReq2Message))]
        public async Task<bool> ClubCreateReq2(GameSession session, ClubCreateReq2Message message)
        {
            var actor = session.Player;
            if (actor == null)
            {
                return false;
            }
            var finalName = !string.IsNullOrWhiteSpace(message.Name)
                ? message.Name.Trim()
                : (message.Unk2 ?? string.Empty).Trim();
            async Task ReplyCreate(int result)
            {
                try
                {
                    await session.SendAsync(new ClubCreateAckMessage(result));
                }
                catch (Exception ex)
                {
                }
                try
                {
                    await session.SendAsync(new ClubCreateAck2Message(result));
                }
                catch (Exception ex)
                {
                }
            }
            try
            {
                if (string.IsNullOrWhiteSpace(finalName))
                {
                    await ReplyCreate(1);
                    return false;
                }
                if (!string.IsNullOrWhiteSpace(message.Unk3))
                {
                    await ReplyCreate(1);
                    return false;
                }
                var duplicate = GameServer.Instance.ClubManager.Any(c =>
                    string.Equals(c.ClanName, finalName, StringComparison.OrdinalIgnoreCase) ||
                    c.Players.ContainsKey(actor.Account.Id));
                if (duplicate)
                {
                    await ReplyCreate(1);
                    return false;
                }
                Club newClub;
                using (var gameDb = GameDatabase.Open())
                {
                    using (var tx = gameDb.BeginTransaction())
                    {
                        try
                        {
                            var accountRow = gameDb.Find<AccountDto>(statement => statement
                                .Where($"{nameof(AccountDto.Id):C} = @Id")
                                .WithParameters(new { Id = actor.Account.Id })
                                .AttachToTransaction(tx))
                                .FirstOrDefault();
                            if (accountRow == null)
                            {
                                tx.Rollback();
                                await ReplyCreate(1);
                                return false;
                            }
                            var clubRow = new ClubDto
                            {
                                Name = finalName,
                                Icon = "0-200-0",
                                Level = 1,
                            };
                            await DbUtil.InsertAsync(gameDb, clubRow, statement =>
                                statement.AttachToTransaction(tx));
                            newClub = new Club(clubRow, new[]
                            {
                        new ClubPlayerInfo
                        {
                            AccountId = actor.Account.Id,
                            Account = accountRow,
                            State = ClubState.Joined,
                            Rank = ClubRank.Master
                        }
                    });
                            await DbUtil.InsertAsync(gameDb, new ClubPlayerDto
                            {
                                PlayerId = (int)actor.Account.Id,
                                ClubId = newClub.Id,
                                Rank = (byte)ClubRank.Master,
                                State = (int)ClubState.Joined
                            }, statement => statement.AttachToTransaction(tx));
                            tx.Commit();
                        }
                        catch (Exception ex)
                        {
                            try
                            {
                                tx.Rollback();
                            }
                            catch (Exception rollbackEx)
                            {
                            }
                            await ReplyCreate(1);
                            return false;
                        }
                    }
                }
                GameServer.Instance.ClubManager.Add(newClub);
                actor.Club = newClub;
                await ReplyCreate(0);
                try
                {
                    await session.SendAsync(new ClubMyInfoAckMessage(actor.Map<Player, ClubMyInfoDto>()));
                }
                catch (Exception ex)
                {
                }
                try
                {
                    Club.LogOn(actor);
                }
                catch (Exception ex)
                {
                }
                return true;
            }
            catch (Exception ex)
            {
                await ReplyCreate(1);
                try
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                }
                catch (Exception sendEx)
                {
                }
                return false;
            }
        }
        [MessageHandler(typeof(ClubRankListReqMessage))]
        public void ClubRankListReq(GameSession session, ClubRankListReqMessage message)
        {
            if (session?.Player == null || message == null)
                return;
            var (total, rankRows) = BuildClubRankListFromDatabase(message.Unk1, message.Unk2);
            if (total == 0)
            {
                session.SendAsync(new ClubRankListAckMessage());
                return;
            }
            session.SendAsync(new ClubRankListAckMessage(total, rankRows));
        }
        internal static (int TotalClans, ClubRankInfoDto[] Clans) BuildClubRankListFromDatabase(
            int offset = 0,
            int limit = 150)
        {
            offset = Math.Max(0, offset);
            limit = limit <= 0 ? 150 : Math.Min(limit, 150);
            using (var gameDb = GameDatabase.Open())
            {
                var rankedClubs = DbUtil.Find<ClubDto>(gameDb)
                    .Where(club => club != null && club.Id > 0)
                    .OrderByDescending(club => club.Points)
                    .ThenByDescending(club => club.Win)
                    .ThenBy(club => club.Loss)
                    .ThenBy(club => club.Id)
                    .ToList();
                var totalClans = rankedClubs.Count;
                if (totalClans == 0)
                    return (0, Array.Empty<ClubRankInfoDto>());
                var allMembers = DbUtil.Find<ClubPlayerDto>(gameDb).ToList();
                var countByClub = allMembers
                    .GroupBy(player => player.ClubId)
                    .ToDictionary(group => group.Key, group => (uint)group.Count());
                var masterByClub = allMembers
                    .Where(player => player.Rank == (int)ClubRank.Master)
                    .GroupBy(player => player.ClubId)
                    .ToDictionary(group => group.Key, group => group.First().PlayerId);
                var masterNameByClub = new Dictionary<uint, string>();
                using (var authDb = AuthDatabase.Open())
                {
                    foreach (var pair in masterByClub)
                    {
                        var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                                .Where($"{nameof(AccountDto.Id):C} = @Id")
                                .WithParameters(new { Id = pair.Value }))
                            .FirstOrDefault();
                        if (acct != null)
                            masterNameByClub[pair.Key] = acct.Nickname ?? "";
                    }
                }
                var output = new List<ClubRankInfoDto>();
                for (var index = 0; index < rankedClubs.Count; index++)
                {
                    if (index < offset)
                        continue;
                    if (output.Count >= limit)
                        break;
                    var clubRow = rankedClubs[index];
                    var positionRank = (uint)(index + 1);
                    countByClub.TryGetValue(clubRow.Id, out var headcount);
                    masterNameByClub.TryGetValue(clubRow.Id, out var leaderName);
                    leaderName ??= "";
                    var livingClub = GameServer.Instance.ClubManager.GetClub(clubRow.Id);
                    output.Add(BuildClubRankInfoDto(clubRow, positionRank, headcount, leaderName, livingClub));
                }
                ClubLog.Information(
                    "ClubRankList offset={Offset} limit={Limit} total={Total} sent={Sent} names=[{Names}]",
                    offset,
                    limit,
                    totalClans,
                    output.Count,
                    string.Join(", ", output.Select(entry => entry.Name)));
                return (totalClans, output.ToArray());
            }
        }
        internal static ClubRankInfoDto BuildClubRankInfoDto(Club club, uint listRank)
        {
            var leader = club.Players?
                .FirstOrDefault(x => x.Value?.Rank == ClubRank.Master).Value;
            var leaderName = leader?.Account?.Nickname ?? "";
            return BuildClubRankInfoDto(
                new ClubDto
                {
                    Id = club.Id,
                    Name = club.ClanName,
                    Icon = club.ClanIcon,
                    Points = club.ClubPoints,
                    Win = club.ClubWin,
                    Loss = club.ClubLoss,
                    Rank = club.ClanRank,
                    Title = club.Title,
                    Message = club.Message
                },
                listRank,
                (uint)Math.Max(0, club.Count),
                leaderName,
                club);
        }
        internal static ClubRankInfoDto BuildClubRankInfoDto(
            ClubDto dto,
            uint listRank,
            uint memberCount,
            string masterName,
            Club liveClub = null)
        {
            var clubDisplayName = liveClub?.ClanName ?? dto.Name ?? "";
            var rawIcon = liveClub?.ClanIcon ?? dto.Icon;
            var safeIcon = string.IsNullOrWhiteSpace(rawIcon) ? "0-200-0" : rawIcon;
            var pts = liveClub?.ClubPoints ?? dto.Points;
            var winCount = liveClub?.ClubWin ?? dto.Win;
            var lossCount = liveClub?.ClubLoss ?? dto.Loss;
            var rankVal = liveClub?.ClanRank ?? dto.Rank;
            if (liveClub != null)
            {
                memberCount = (uint)Math.Max(0, liveClub.Count);
                var leader = liveClub.Players?
                    .FirstOrDefault(x => x.Value?.Rank == ClubRank.Master).Value;
                if (!string.IsNullOrEmpty(leader?.Account?.Nickname))
                    masterName = leader.Account.Nickname;
            }
            masterName ??= "";
            return new ClubRankInfoDto
            {
                ClanId = dto.Id,
                Name = clubDisplayName,
                ClanIcon = safeIcon,
                Unk2 = masterName,
                Unk3 = listRank,
                Unk4 = memberCount,
                Unk5 = pts,
                MasterName = masterName,
                Unk6 = 1,
                Unk7 = memberCount,
                Unk8 = "",
                CreationDate = "",
                Unk9 = liveClub?.Title ?? dto.Title ?? "",
                Unk10 = liveClub?.Message ?? dto.Message ?? "",
                Unk11 = (int)listRank,
                Unk15 = (int)pts,
                Unk16 = (int)rankVal,
                Unk17 = (int)winCount,
                Unk18 = (int)lossCount,
                Unk19 = (int)rankVal,
            };
        }
        private sealed class ResolvedClubSnapshot
        {
            public ClubDto Dto { get; set; }
            public Club LiveClub { get; set; }
            public uint ListRank { get; set; }
            public uint MemberCount { get; set; }
            public string MasterName { get; set; } = "";
            public ulong MasterAccountId { get; set; }
        }
        private static bool TryResolveClubSnapshot(
            out ResolvedClubSnapshot snapshot,
            string clubName = null,
            uint clubId = 0)
        {
            snapshot = null;
            using (var gameDb = GameDatabase.Open())
            {
                var rankedClubs = DbUtil.Find<ClubDto>(gameDb)
                    .Where(club => club != null && club.Id > 0)
                    .OrderByDescending(club => club.Points)
                    .ThenByDescending(club => club.Win)
                    .ThenBy(club => club.Loss)
                    .ThenBy(club => club.Id)
                    .ToList();
                ClubDto clubRow = null;
                if (clubId > 0)
                    clubRow = rankedClubs.FirstOrDefault(club => club.Id == clubId);
                var needle = clubName?.Trim();
                if (clubRow == null && !string.IsNullOrWhiteSpace(needle))
                {
                    clubRow = rankedClubs.FirstOrDefault(club =>
                        string.Equals(club.Name, needle, StringComparison.OrdinalIgnoreCase));
                }
                if (clubRow == null && !string.IsNullOrWhiteSpace(needle))
                {
                    var liveMatch = GameServer.Instance.ClubManager
                        .FirstOrDefault(club =>
                            string.Equals(club.ClanName, needle, StringComparison.OrdinalIgnoreCase));
                    if (liveMatch == null)
                        return false;
                    clubRow = new ClubDto
                    {
                        Id = liveMatch.Id,
                        Name = liveMatch.ClanName,
                        Icon = liveMatch.ClanIcon,
                        Level = liveMatch.Level,
                        Exp = liveMatch.Exp,
                        Rank = liveMatch.ClanRank,
                        Points = liveMatch.ClubPoints,
                        Win = liveMatch.ClubWin,
                        Loss = liveMatch.ClubLoss,
                        Title = liveMatch.Title,
                        Message = liveMatch.Message
                    };
                }
                if (clubRow == null)
                    return false;
                return TryBuildClubSnapshot(gameDb, rankedClubs, clubRow, out snapshot);
            }
        }
        private static bool TryBuildClubSnapshot(
            System.Data.IDbConnection gameDb,
            IReadOnlyList<ClubDto> rankedClubs,
            ClubDto clubRow,
            out ResolvedClubSnapshot snapshot)
        {
            snapshot = null;
            if (clubRow == null || clubRow.Id == 0)
                return false;
            var positionRank = 1u;
            for (var index = 0; index < rankedClubs.Count; index++)
            {
                if (rankedClubs[index].Id == clubRow.Id)
                {
                    positionRank = (uint)(index + 1);
                    break;
                }
            }
            var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                    .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                    .WithParameters(new { ClubId = clubRow.Id }))
                .ToList();
            var headcount = (uint)Math.Max(0, roster.Count);
            var leaderRow = roster.FirstOrDefault(player => player.Rank == (int)ClubRank.Master)
                               ?? roster.FirstOrDefault();
            var leaderName = "";
            ulong leaderAccountId = 0;
            if (leaderRow != null)
            {
                leaderAccountId = (ulong)leaderRow.PlayerId;
                using (var authDb = AuthDatabase.Open())
                {
                    var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                            .Where($"{nameof(AccountDto.Id):C} = @Id")
                            .WithParameters(new { Id = leaderRow.PlayerId }))
                        .FirstOrDefault();
                    leaderName = acct?.Nickname ?? "";
                }
            }
            var livingClub = GameServer.Instance.ClubManager.GetClub(clubRow.Id);
            if (livingClub != null)
            {
                headcount = (uint)Math.Max(0, livingClub.Count);
                var liveLeader = livingClub.Players.Values
                    .FirstOrDefault(player => player.Rank == ClubRank.Master);
                if (!string.IsNullOrEmpty(liveLeader?.Account?.Nickname))
                {
                    leaderName = liveLeader.Account.Nickname;
                    leaderAccountId = liveLeader.AccountId;
                }
            }
            snapshot = new ResolvedClubSnapshot
            {
                Dto = clubRow,
                LiveClub = livingClub,
                ListRank = positionRank,
                MemberCount = headcount,
                MasterName = leaderName ?? "",
                MasterAccountId = leaderAccountId
            };
            return true;
        }
        private static ClubOtherClubInfo BuildClubOtherClubInfo(ResolvedClubSnapshot snapshot)
        {
            var row = snapshot.Dto;
            var living = snapshot.LiveClub;
            var pts = living?.ClubPoints ?? row.Points;
            var winCount = living?.ClubWin ?? row.Win;
            var lossCount = living?.ClubLoss ?? row.Loss;
            var rankVal = living?.ClanRank ?? row.Rank;
            var clubLevel = living?.Level ?? row.Level;
            var capacity = Math.Max(clubLevel * 12, 12);
            var rawCount = (int)snapshot.MemberCount;
            var clientCount = Math.Min(rawCount, 29);
            var motto = living?.Title ?? row.Title ?? "";
            var announce = living?.Message ?? row.Message ?? "";
            var payload = new ClubOtherClubInfo
            {
                Unk1 = (int)row.Id,
                Unk2 = living?.ClanName ?? row.Name ?? "",
                Unk3 = GetSafeClanIcon(living?.ClanIcon ?? row.Icon),
                Unk4 = snapshot.MasterName,
                Unk5 = snapshot.MasterAccountId,
                Unk6 = clubLevel,
                Unk7 = motto,
                Unk8 = clientCount,
                Unk9 = capacity,
                Unk10 = motto,
                Unk11 = announce,
                Unk12 = "",
                Unk13 = "",
                Unk14 = (int)rankVal,
                Unk15 = 0,
                Unk16 = (int)(winCount + lossCount),
                Unk17 = (int)pts,
                Unk18 = (int)pts,
                Unk19 = (int)rankVal,
                Unk20 = (int)winCount,
                Unk21 = (int)lossCount,
                Unk22 = (int)rankVal,
                Unk23 = 0,
                Unk24 = 0
            };
            return payload;
        }
        private static ClubInfoDto BuildClubInfoDtoFromSnapshot(ResolvedClubSnapshot snapshot)
        {
            var row = snapshot.Dto;
            var living = snapshot.LiveClub;
            var fight = GetClubFightStats(snapshot);
            var motto = living?.Title ?? row.Title ?? "";
            var announce = living?.Message ?? row.Message ?? "";
            return new ClubInfoDto
            {
                Id = row.Id,
                Name = living?.ClanName ?? row.Name ?? "",
                Type = GetSafeClanIcon(living?.ClanIcon ?? row.Icon),
                MasterName = snapshot.MasterName,
                MemberCount = (int)snapshot.MemberCount,
                Unk1 = fight.Points,
                Unk2 = fight.ClanRank,
                Unk3 = fight.Wins,
                Unk4 = fight.Losses,
                Motto = motto,
                Announce = announce
            };
        }
        private static ClubInfoDto2 BuildClubInfoDto2FromSnapshot(ResolvedClubSnapshot snapshot)
        {
            var row = snapshot.Dto;
            var living = snapshot.LiveClub;
            var fight = GetClubFightStats(snapshot);
            var motto = living?.Title ?? row.Title ?? "";
            var announce = living?.Message ?? row.Message ?? "";
            var battleTotal = fight.Wins + fight.Losses;
            return new ClubInfoDto2
            {
                Id = row.Id,
                Id2 = row.Id,
                Name = living?.ClanName ?? row.Name ?? "",
                Type = GetSafeClanIcon(living?.ClanIcon ?? row.Icon),
                MasterName = snapshot.MasterName,
                MemberCount = snapshot.MemberCount,
                Motto = motto,
                Unk4 = (uint)fight.ClanRank,
                Unk5 = (uint)fight.Points,
                Unk7 = (uint)fight.Wins,
                Unk8 = (uint)fight.Losses,
                Unk10 = motto,
                Unk11 = announce,
                Unk16 = (uint)fight.Points,
                Unk17 = (uint)fight.ClanRank,
                Unk18 = (uint)battleTotal,
                Unk19 = (uint)fight.Wins,
                Unk20 = (uint)fight.Points,
                Unk21 = (uint)fight.Losses,
                Unk22 = (ushort)fight.ClanRank
            };
        }
        private static string GetClubNameById(uint clubId)
        {
            var living = GameServer.Instance.ClubManager.GetClub(clubId);
            if (!string.IsNullOrWhiteSpace(living?.ClanName))
                return living.ClanName;
            using (var gameDb = GameDatabase.Open())
            {
                var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubDto.Id):C} = @Id")
                        .WithParameters(new { Id = clubId }))
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(clubRow?.Name))
                    return clubRow.Name;
            }
            return $"Club{clubId}";
        }
        private static string[] GetClubMemberNicknames(uint clubId, int maxCount = 4)
        {
            var names = new List<string>();
            var living = GameServer.Instance.ClubManager.GetClub(clubId);
            if (living != null)
            {
                names.AddRange(living.Players.Values
                    .Select(player => player.Account?.Nickname)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Take(maxCount)
                    .Cast<string>());
            }
            if (names.Count >= maxCount)
                return names.ToArray();
            using (var gameDb = GameDatabase.Open())
            {
                var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                        .WithParameters(new { ClubId = clubId }))
                    .OrderBy(player => player.Rank)
                    .ThenBy(player => player.PlayerId)
                    .Take(maxCount)
                    .ToList();
                using (var authDb = AuthDatabase.Open())
                {
                    foreach (var memberRow in roster)
                    {
                        var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                                .Where($"{nameof(AccountDto.Id):C} = @Id")
                                .WithParameters(new { Id = memberRow.PlayerId }))
                            .FirstOrDefault();
                        var nick = acct?.Nickname;
                        if (!string.IsNullOrWhiteSpace(nick) && !names.Contains(nick))
                            names.Add(nick);
                        if (names.Count >= maxCount)
                            break;
                    }
                }
            }
            if (names.Count == 0)
                names.Add("?");
            return names.ToArray();
        }
        private static (int Points, int Battles, int Wins, int Losses, int ClanRank) GetClubFightStats(ResolvedClubSnapshot snapshot)
        {
            var row = snapshot.Dto;
            var living = snapshot.LiveClub;
            var winCount = (int)(living?.ClubWin ?? row.Win);
            var lossCount = (int)(living?.ClubLoss ?? row.Loss);
            var pts = (int)(living?.ClubPoints ?? row.Points);
            var rankVal = (int)(living?.ClanRank ?? row.Rank);
            return (pts, winCount + lossCount, winCount, lossCount, rankVal);
        }
        private static ClubNoticeRecordDto[] BuildClubNoticeRecords(
            uint clubId,
            string clubName,
            int wins,
            int losses)
        {
            if (string.IsNullOrWhiteSpace(clubName))
                clubName = "MyClub";
            var rows = new List<ClubNoticeRecordDto>();
            try
            {
                using (var gameDb = GameDatabase.Open())
                {
                    var historyRows = DbUtil.Find<ClanHistoryDto>(gameDb, statement => statement
                            .Where($"{nameof(ClanHistoryDto.ClubId):C} = @ClubId")
                            .WithParameters(new { ClubId = clubId }))
                        .OrderByDescending(x => x.Id)
                        .Take(5)
                        .ToList();
                    foreach (var h in historyRows)
                    {
                        var enemyName = GetClubNameById(h.EnemyClanId);
                        var won = h.Status.Equals("Won", StringComparison.OrdinalIgnoreCase) ||
                                    h.Status.Equals("Win", StringComparison.OrdinalIgnoreCase);
                        var modeId = h.GameMode > 0 ? h.GameMode : 1;
                        var mapNo = h.MapId > 0 ? h.MapId : 2;
                        var ourIds = SplitClanHistoryPlayers(h.ClanPlayers);
                        var foeIds = SplitClanHistoryPlayers(h.EnemyClanPlayers);
                        var ourNames = BuildClanHistoryPlayerNames(ourIds);
                        var foeNames = BuildClanHistoryPlayerNames(foeIds);
                        rows.Add(new ClubNoticeRecordDto
                        {
                            Unk1 = enemyName,
                            Unk2 = clubName,
                            Unk3 = modeId,
                            Unk4 = mapNo,
                            Unk5 = won ? 1 : 0,
                            Unk6 = h.Id,
                            Unk7 = ourNames,
                            Unk8 = foeNames
                        });
                    }
                }
            }
            catch (Exception ex)
            {
            }
            if (rows.Count == 0)
            {
            }
            return rows.ToArray();
        }
        private static string[] SplitClanHistoryPlayers(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return Array.Empty<string>();
            return value
                .Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .ToArray();
        }
        private static string[] BuildClanHistoryPlayerNames(string[] playerIds)
        {
            if (playerIds == null || playerIds.Length == 0)
                return Array.Empty<string>();
            return playerIds
                .Select(GetClanHistoryPlayerName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToArray();
        }
        private static string GetClanHistoryPlayerName(string playerId)
        {
            if (ulong.TryParse(playerId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedId))
            {
                var nick = GetAccountNickname(parsedId);
                if (!string.IsNullOrWhiteSpace(nick))
                    return nick;
            }
            return playerId;
        }
        private static JoinWaiterInfoDto BuildJoinWaiterInfoDto(ulong accountId, AccountDto account, PlayerDto playerDto)
        {
            var displayNick = account?.Nickname ?? accountId.ToString(CultureInfo.InvariantCulture);
            var lvl = playerDto?.Level ?? 0;
            var winTotal = Math.Max(0, playerDto?.TotalWins ?? 0);
            var lossTotal = Math.Max(0, playerDto?.TotalLosses ?? 0);
            var battleTotal = winTotal + lossTotal;
            var rankLabel = GetJoinWaiterRankText(lvl);
            return new JoinWaiterInfoDto
            {
                Unk1 = accountId,
                Unk2 = displayNick,
                Unk3 = lvl,
                Unk4 = 30,
                Unk5 = "",
                Unk6 = "",
                Unk7 = "",
                Unk8 = "",
                Unk9 = "",
                Unk10 = "",
                Unk11 = "",
                Unk12 = "",
                Unk13 = "",
                Unk14 = "",
                Unk15 = "",
                Unk16 = winTotal,
                Unk17 = lossTotal,
                Unk18 = battleTotal,
                Unk19 = 0
            };
        }
        private static JoinWaiterInfoDto[] LoadJoinWaiters(uint clubId)
        {
            using (var gameDb = GameDatabase.Open())
            using (var authDb = AuthDatabase.Open())
            {
                var requestRows = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                        .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId")
                        .WithParameters(new { ClubId = clubId }))
                    .OrderBy(x => x.Id)
                    .ToList();
                return requestRows
                    .Select(req =>
                    {
                        var acctId = (ulong)req.PlayerId;
                        var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                                .Where($"{nameof(AccountDto.Id):C} = @Id")
                                .WithParameters(new { Id = acctId }))
                            .FirstOrDefault();
                        var pRow = DbUtil.Find<PlayerDto>(gameDb, statement => statement
                                .Where($"{nameof(PlayerDto.Id):C} = @Id")
                                .WithParameters(new { Id = (int)acctId }))
                            .FirstOrDefault();
                        return BuildJoinWaiterInfoDto(acctId, acct, pRow);
                    })
                    .Where(x => x != null)
                    .ToArray();
            }
        }
        private static string GetJoinWaiterRankText(byte level)
        {
            if (level >= 61)
                return "S-Class";
            if (level >= 51)
                return "A-Class";
            if (level >= 41)
                return "B-Class";
            if (level >= 31)
                return "C-Class";
            if (level >= 21)
                return "Semi Pro";
            if (level >= 11)
                return "Amateur";
            return "Rookie";
        }
        private static string GetAccountNickname(ulong accountId)
        {
            var onlineMember = GameServer.Instance.PlayerManager.Get(accountId);
            if (!string.IsNullOrWhiteSpace(onlineMember?.Account?.Nickname))
                return onlineMember.Account.Nickname;
            using (var authDb = AuthDatabase.Open())
            {
                var acct = DbUtil.Find<AccountDto>(authDb, statement => statement
                        .Where($"{nameof(AccountDto.Id):C} = @Id")
                        .WithParameters(new { Id = accountId }))
                    .FirstOrDefault();
                return acct?.Nickname ?? "";
            }
        }
        private static async Task NotifyClubJoinWaitersChanged(uint clubId, string source)
        {
            var clan = GameServer.Instance.ClubManager.GetClub(clubId);
            if (clan == null)
            {
                return;
            }
            var waiterRows = LoadJoinWaiters(clubId);
            var deliveries = 0;
            foreach (var staffMember in clan.Players.Values.Where(x => x.Rank <= ClubRank.Staff))
            {
                var staffOnline = GameServer.Instance.PlayerManager.Get(staffMember.AccountId);
                if (staffOnline == null)
                    continue;
                await staffOnline.SendAsync(new ClubJoinWaiterInfoAckMessage(waiterRows));
                deliveries++;
            }
        }
        private static async Task SendClubJoinDecisionMessage(Player sender, Club club, ulong targetId, bool accepted)
        {
            if (sender?.Mailbox == null || club == null)
                return;
            var targetNick = GetAccountNickname(targetId);
            if (string.IsNullOrWhiteSpace(targetNick))
            {
                return;
            }
            var body = accepted
                ? $"Your request to join {club.ClanName} has been approved."
                : $"Your request to join {club.ClanName} has been rejected.";
            var subject = accepted ? "Approved" : "Rejected";
            var delivered = await sender.Mailbox.SendTypedAsync(targetNick, subject, body, 2);
        }
        private static void ClearViewingOtherClub(Player actor)
        {
            if (actor == null)
                return;
            actor.ViewingOtherClubId = 0;
            actor.PreferOtherClubNotice = false;
        }
        private static void BeginOtherClubVisit(Player actor, uint clubId)
        {
            if (actor == null)
                return;
            actor.ViewingOtherClubId = clubId;
            actor.PreferOtherClubNotice = true;
            actor.LastOtherClubInfoUtc = DateTime.UtcNow;
        }
        private static bool ShouldUseOtherClubNotice(Player actor)
        {
            return actor != null && actor.PreferOtherClubNotice && actor.ViewingOtherClubId > 0;
        }
        private static void TouchOtherClubVisit(Player actor)
        {
            if (actor != null)
                actor.LastOtherClubInfoUtc = DateTime.UtcNow;
        }
        private static async Task SendOtherClubPointRefreshAsync(GameSession session, ResolvedClubSnapshot snapshot)
        {
            var actor = session.Player;
            if (actor == null)
                return;
            var row = snapshot.Dto;
            var fight = GetClubFightStats(snapshot);
            await session.SendAsync(new ClubNotice_Point_Refresh_Ack
            {
                Unk1 = fight.Points,
                Unk2 = row.Id,
                Unk3 = actor.Account.Id,
                Unk4 = new[] { fight.ClanRank }
            });
            await session.SendAsync(new ClubClubInfoAckMessage(BuildClubInfoDtoFromSnapshot(snapshot)));
            await session.SendAsync(new ClubClubInfoAck2Message(BuildClubInfoDto2FromSnapshot(snapshot)));
        }
        private static async Task SendOtherClubRecordRefreshAsync(
            GameSession session,
            ResolvedClubSnapshot snapshot)
        {
            var row = snapshot.Dto;
            var living = snapshot.LiveClub;
            var name = living?.ClanName ?? row.Name ?? "";
            var fight = GetClubFightStats(snapshot);
            var rows = BuildClubNoticeRecords(row.Id, name, fight.Wins, fight.Losses);
            await session.SendAsync(new ClubNotice_Record_Refresh_Ack
            {
                Unk1 = 0,
                Info = rows
            });
        }
        internal static async Task SendOwnClubOverviewOnLoginAsync(GameSession session)
        {
            var actor = session?.Player;
            if (actor?.Club == null)
                return;
            if (!TryResolveClubSnapshot(out var snap, clubId: actor.Club.Id))
                return;
            BeginOtherClubVisit(actor, snap.Dto.Id);
            await session.SendAsync(new ClubOtherClubInfoAckMessage(0, BuildClubOtherClubInfo(snap)));
        }
        [MessageHandler(typeof(ClubAddressReqMessage))]
        public void CClubAddressReq(GameSession session, ClubAddressReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            session.SendAsync(new ClubAddressAckMessage("", 0));
        }
        [MessageHandler(typeof(ClubClubMemberInfoReq2Message))]
        public void ClubClubMemberInfoReq2(ChatSession session, ClubClubMemberInfoReq2Message message)
        {
            if (session == null || message == null || message.AccountId == 0)
            {
                session?.SendAsync(new ClubClubMemberInfoAck2Message { ClanId = message?.ClanId ?? 0, AccountId = 0, Nickname = "n/A" });
                return;
            }
            var targetMember = GameServer.Instance.PlayerManager[message.AccountId];
            if (targetMember?.Club != null)
            {
                var isMaster = targetMember.Club.Players.Any(x => x.Value.Rank == ClubRank.Master && x.Key == targetMember.Account.Id);
                session.SendAsync(new ClubClubMemberInfoAck2Message
                {
                    ClanId = message.ClanId,
                    AccountId = targetMember.Account.Id,
                    Nickname = targetMember.Account.Nickname,
                    IsModerator = isMaster ? 1 : 0
                });
            }
            else if (session.Player != null && targetMember != null)
            {
                session.SendAsync(new ClubClubMemberInfoAck2Message
                {
                    ClanId = message.ClanId,
                    AccountId = targetMember.Account.Id,
                    Nickname = targetMember.Account.Nickname
                });
            }
            else
            {
                session.SendAsync(new ClubClubMemberInfoAck2Message
                {
                    ClanId = message.ClanId,
                    AccountId = 0,
                    Nickname = "n/A"
                });
            }
        }
        [MessageHandler(typeof(ClubMemberListReqMessage))]
        public async Task ClubMemberListReq(ChatSession session, ClubMemberListReqMessage message)
        {
            if (session == null || message == null)
            {
                return;
            }
            var actor = session.Player;
            if (actor?.Club == null)
            {
                if (actor?.ChatSession != null)
                    await actor.ChatSession.SendAsync(new ClubMemberListAckMessage());
                return;
            }
            var clan = message.ClanId > 0 ? GameServer.Instance.ClubManager.GetClub(message.ClanId) : actor.Club;
            if (clan == null || clan.Id != actor.Club.Id)
            {
                if (actor.ChatSession != null)
                    await actor.ChatSession.SendAsync(new ClubMemberListAckMessage());
                return;
            }
            var memberDtos = new List<ClubMemberDto>();
            foreach (var entry in clan.Players.Values)
            {
                var onlineMember = GameServer.Instance.PlayerManager
                    .FirstOrDefault(p => p.Account.Id == entry.AccountId);
                if (onlineMember != null)
                    memberDtos.Add(onlineMember.Map<Player, ClubMemberDto>());
                else
                    memberDtos.Add(entry.Map<ClubPlayerInfo, ClubMemberDto>());
            }
            if (actor.ChatSession != null)
                await actor.ChatSession.SendAsync(new ClubMemberListAckMessage(memberDtos.ToArray()));
            foreach (var entry in clan.Players.Values)
            {
                var onlineMember = GameServer.Instance.PlayerManager
                    .FirstOrDefault(p => p.Account.Id == entry.AccountId);
                var present = onlineMember != null;
                if (!present)
                    continue;
                try
                {
                    if (actor.ChatSession != null)
                        await actor.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(2, entry.AccountId));
                }
                catch (Exception ex)
                {
                }
                try
                {
                    await actor.SendAsync(new ClubMemberLoginStateAckMessage(2, entry.AccountId));
                }
                catch (Exception ex)
                {
                }
            }
            try
            {
                await CommunityService.SendCombiList(actor);
            }
            catch (Exception ex)
            {
            }
           ;
        }
        [MessageHandler(typeof(ClubMemberListReq2Message))]
        public async Task ClubMemberListReq2(ChatSession session, ClubMemberListReq2Message message)
        {
            if (session == null || message == null)
            {
                return;
            }
            var actor = session.Player;
            if (actor?.Club == null)
            {
                if (actor?.ChatSession != null)
                    await actor.ChatSession.SendAsync(new ClubMemberListAck2Message());
                return;
            }
            var clan = actor.Club;
            var memberDtos = new List<ClubMemberDto2>();
            foreach (var entry in clan.Players.Values)
            {
                var onlineMember = GameServer.Instance.PlayerManager
                    .FirstOrDefault(p => p.Account.Id == entry.AccountId);
                if (onlineMember != null)
                    memberDtos.Add(onlineMember.Map<Player, ClubMemberDto2>());
                else
                    memberDtos.Add(entry.Map<ClubPlayerInfo, ClubMemberDto2>());
            }
            if (actor.ChatSession != null)
                await actor.ChatSession.SendAsync(new ClubMemberListAck2Message(clan.Id, memberDtos.ToArray()));
            foreach (var entry in clan.Players.Values)
            {
                var onlineMember = GameServer.Instance.PlayerManager
                    .FirstOrDefault(p => p.Account.Id == entry.AccountId);
                var present = onlineMember != null;
                if (!present)
                    continue;
                try
                {
                    if (actor.ChatSession != null)
                        await actor.ChatSession.SendAsync(new ClubMemberLoginStateAckMessage(1, entry.AccountId));
                }
                catch (Exception ex)
                {
                }
                try
                {
                    await actor.SendAsync(new ClubMemberLoginStateAckMessage(1, entry.AccountId));
                }
                catch (Exception ex)
                {
                }
            }
            try
            {
            }
            catch (Exception ex)
            {
            }
        }
        [MessageHandler(typeof(ClubNoteSendReq2Message))]
        public void ClubNoteSendReq2(ChatSession session, ClubNoteSendReq2Message message)
        {
            session.GameSession?.SendAsync(new ClubNoteSendAckMessage { Unk = 1 });
        }
        [MessageHandler(typeof(ClubUnjoinReqMessage))]
        public async Task ClubUnjoinReq(GameSession session, ClubUnjoinReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            await ClubUnjoinReq2(session, message.Map<ClubUnjoinReqMessage, ClubUnjoinReq2Message>());
        }
        [MessageHandler(typeof(ClubUnjoinReq2Message))]
        public async Task ClubUnjoinReq2(GameSession session, ClubUnjoinReq2Message message)
        {
            if (session?.Player == null || message == null || message.ClanId <= 0)
            {
                session?.SendAsync(new ClubUnjoinAck2Message(4));
                return;
            }
            var actor = session.Player;
            if (actor?.Club == null || actor.Club.Id != message.ClanId)
            {
                await session.SendAsync(new ClubUnjoinAck2Message(4));
                return;
            }
            {
                if (actor.Club.Players.Values.Any(x => x.Account?.Id == (int)actor.Account.Id && x.Rank != ClubRank.Master))
                {
                    using (var gameDb = GameDatabase.Open())
                    {
                        var clubRow = (await DbUtil.FindAsync<ClubDto>(gameDb, statement => statement
                            .Where($"{nameof(ClubDto.Id):C} = @Id")
                            .WithParameters(new { actor.Club.Id }))).FirstOrDefault();
                        if (clubRow != null)
                        {
                            var membershipRow = (await gameDb.FindAsync<ClubPlayerDto>(statement => statement
                                    .Where($"{nameof(ClubPlayerDto.ClubId):C} = @Id")
                                    .WithParameters(new { actor.Club.Id })))
                                .FirstOrDefault(x => x.PlayerId == (int)actor.Account.Id);
                            if (membershipRow != null)
                            {
                                var leavingClan = actor.Club;
                                var deliveries = 0;
                                foreach (var staffMember in leavingClan.Players.Values.Where(x =>
                                             x.AccountId != actor.Account.Id && x.Rank <= ClubRank.Staff))
                                {
                                    var staffNick = staffMember.Account?.Nickname;
                                    if (string.IsNullOrWhiteSpace(staffNick))
                                        staffNick = GetAccountNickname(staffMember.AccountId);
                                    if (string.IsNullOrWhiteSpace(staffNick))
                                        continue;
                                    var delivered = await actor.Mailbox.SendTypedAsync(
                                        staffNick,
                                        "Leave",
                                        $"The player {actor.Account.Nickname} has left the club.",
                                        2);
                                    if (delivered)
                                        deliveries++;
                                }
                                Club.LogOff(actor);
                                actor.Club.Players.TryRemove(actor.Account.Id, out var _);
                                gameDb.Delete(new ClubPlayerDto() { PlayerId = membershipRow.PlayerId });
                                actor.Club = null;
                                await session.SendAsync(new ClubMyInfoAckMessage(actor.Map<Player, ClubMyInfoDto>()));
                                await session.SendAsync(new ClubUnjoinAck2Message());
                            }
                            else
                            {
                                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                            }
                        }
                        else
                        {
                            await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                        }
                    }
                }
                else
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                }
            }
        }
        [MessageHandler(typeof(ClubCloseReqMessage))]
        public async Task ClubCloseReq(GameSession session, ClubCloseReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            await ClubCloseReq2(
                session,
                message.Map<ClubCloseReqMessage, ClubCloseReq2Message>()
            );
        }
        [MessageHandler(typeof(ClubCloseReq2Message))]
        public async Task ClubCloseReq2(GameSession session, ClubCloseReq2Message message)
        {
            async Task ReplyClose(int result)
            {
                if (result == 0)
                {
                    try
                    {
                        await session.SendAsync(new ClubCloseAckMessage());
                    }
                    catch (Exception ex)
                    {
                    }
                }
                try
                {
                    await session.SendAsync(new ClubCloseAck2Message(result));
                }
                catch (Exception ex)
                {
                }
            }
            if (session?.Player == null || message == null || message.ClanId <= 0)
            {
                await ReplyClose(1);
                return;
            }
            var actor = session.Player;
            if (actor.Club == null)
            {
                await ReplyClose(1);
                return;
            }
            if (actor.Club.Id != message.ClanId)
            {
                await ReplyClose(1);
                return;
            }
            var ownerIsMaster = actor.Club.Players.Any(x =>
                x.Key == actor.Account.Id &&
                x.Value.Rank == ClubRank.Master);
            if (!ownerIsMaster)
            {
                await ReplyClose(1);
                return;
            }
            var headcount = actor.Club.Players.Count;
            if (headcount > 1)
            {
                await ReplyClose(3);
                return;
            }
            var closingId = actor.Club.Id;
            var closingClan = actor.Club;
            using (var gameDb = GameDatabase.Open())
            {
                using (var tx = gameDb.BeginTransaction())
                {
                    try
                    {
                        var clubRow = (await gameDb.FindAsync<ClubDto>(statement => statement
                            .Where($"{nameof(ClubDto.Id):C} = @Id")
                            .WithParameters(new { Id = closingId })
                            .AttachToTransaction(tx)))
                            .FirstOrDefault();
                        if (clubRow == null)
                        {
                            tx.Rollback();
                            await ReplyClose(1);
                            return;
                        }
                        gameDb.Execute(
                            "DELETE FROM club_players WHERE ClubId = @ClubId",
                            new { ClubId = closingId },
                            tx
                        );
                        gameDb.Execute(
                            "DELETE FROM clan_union WHERE ClubId = @ClubId OR UnionId = @ClubId",
                            new { ClubId = closingId },
                            tx
                        );
                        gameDb.Execute(
                            "DELETE FROM clubs WHERE Id = @Id",
                            new { Id = closingId },
                            tx
                        );
                        tx.Commit();
                    }
                    catch (Exception ex)
                    {
                        try
                        {
                            tx.Rollback();
                        }
                        catch (Exception rollbackEx)
                        {
                        }
                        await ReplyClose(1);
                        return;
                    }
                }
            }
            foreach (var slot in closingClan.Players.ToArray())
            {
                closingClan.Players.TryRemove(slot.Key, out _);
            }
            GameServer.Instance.ClubManager.Remove(closingClan);
            await ReplyClose(0);
            foreach (var affected in GameServer.Instance.PlayerManager.Where(x => x.Club?.Id == closingId).ToArray())
            {
                Club.LogOff(affected);
                affected.Club = null;
                if (affected.Session != null)
                {
                    await affected.Session.SendAsync(
                        new ClubMyInfoAckMessage(affected.Map<Player, ClubMyInfoDto>())
                    );
                }
            }
        }
        [MessageHandler(typeof(ClubAdminMasterChangeReqMessage))]
        public async Task ClubAdminMasterChangeReq(GameSession session, ClubAdminMasterChangeReqMessage message)
        {
            if (session?.Player == null || message == null || message.Target == 0)
            {
                return;
            }
            var actor = session.Player;
            var clan = actor.Club;
            if (clan == null || clan.Id <= 0)
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.NotInClan));
                return;
            }
            if (!clan.Players.TryGetValue(actor.Account.Id, out var senderSlot))
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            if (senderSlot.Rank != ClubRank.Master)
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            if (!clan.Players.TryGetValue(message.Target, out var targetSlot))
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            if (targetSlot.AccountId == actor.Account.Id)
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            if (targetSlot.Rank != ClubRank.Staff)
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            var changed = await clan.ChangeMaster(actor, message.Target);
            if (!changed)
            {
                await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.MemberNotHaveAuthority));
                return;
            }
            await actor.SendAsync(new ClubAdminMasterChangeAckMessage(ClanMasterChangeMessage.Ok));
            var promotedNick = targetSlot.Account?.Nickname;
            if (string.IsNullOrWhiteSpace(promotedNick))
                promotedNick = GetAccountNickname(targetSlot.AccountId);
            if (actor.Mailbox != null && !string.IsNullOrWhiteSpace(promotedNick))
            {
                var delivered = await actor.Mailbox.SendTypedAsync(
                    promotedNick,
                    "Clan Master",
                    "Your have been promoted to Clan Master.",
                    2);
                foreach (var otherMember in clan.Players.Values.ToArray())
                {
                    if (otherMember.AccountId == targetSlot.AccountId)
                        continue;
                    var otherNick = otherMember.Account?.Nickname;
                    if (string.IsNullOrWhiteSpace(otherNick))
                        otherNick = GetAccountNickname(otherMember.AccountId);
                    if (string.IsNullOrWhiteSpace(otherNick))
                        continue;
                    delivered = await actor.Mailbox.SendTypedAsync(
                        otherNick,
                        "Clan Master",
                        $"{promotedNick} was promoted to Clan Master.",
                        2);
                }
            }
            foreach (var onlineMember in GameServer.Instance.PlayerManager.Where(x => x.Club?.Id == clan.Id).ToArray())
            {
                try
                {
                    await onlineMember.SendAsync(new ClubMyInfoAckMessage(onlineMember.Map<Player, ClubMyInfoDto>()));
                }
                catch (Exception ex)
                {
                }
            }
        }
        [MessageHandler(typeof(ClubNewJoinMemberInfoReqMessage))]
        public async Task ClubNewJoinMemberInfoReqMessage(GameSession session, ClubNewJoinMemberInfoReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var actor = session.Player;
            var clan = actor.Club;
            if (clan == null || actor.ChatSession == null)
            {
                return;
            }
            var memberDtos = new List<ClubMemberInfoDto>();
            memberDtos.AddRange(GameServer.Instance.PlayerManager
            .Where(p => actor.Club.Players.Keys.Contains(p.Account.Id))
            .Select(p => p.Map<Player, ClubMemberInfoDto>()));
            memberDtos.AddRange(actor.Club.Players.Select(x => x.Value.Map<ClubPlayerInfo, ClubMemberInfoDto>()));
            await actor.ChatSession.SendAsync(new ClubNewJoinMemberInfoAckMessage(memberDtos.ToArray()));
        }
        [MessageHandler(typeof(ClubAdminJoinCommandReqMessage))]
        public async Task ClubAdminJoinCommandReq(GameSession session, ClubAdminJoinCommandReqMessage message)
        {
            if (session?.Player == null || message?.AccountId == null || message.AccountId.Length == 0 || message.AccountId.Length > 32)
            {
                session?.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                return;
            }
            var actor = session.Player;
            var clan = actor.Club;
            if (clan == null || clan.Id <= 0 || !clan.Players.TryGetValue(actor.Account.Id, out var senderSlot) || senderSlot.Rank > ClubRank.Staff)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                return;
            }
            var target = message.AccountId[0];
            var cmd = (ClubCommand)message.Command;
            var outcome = ClubCommandResult.Success;
            var notifyDecision = false;
            var wasAccepted = false;
            using (var gameDb = GameDatabase.Open())
            {
                if (cmd == ClubCommand.Kick || cmd == ClubCommand.Ban)
                {
                    var targetRow = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                            .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId AND {nameof(ClubPlayerDto.PlayerId):C} = @PlayerId")
                            .WithParameters(new { ClubId = clan.Id, PlayerId = target }))
                        .FirstOrDefault();
                    if (targetRow == null)
                    {
                        outcome = ClubCommandResult.MemberNotFound;
                    }
                    else if (target == actor.Account.Id || (ClubRank)targetRow.Rank <= senderSlot.Rank)
                    {
                        outcome = ClubCommandResult.PermissionDenied;
                    }
                    else
                    {
                        var kicked = await clan.RemoveKickPlayer(target, cmd == ClubCommand.Ban);
                        outcome = kicked ? ClubCommandResult.Success : ClubCommandResult.MemberNotFound;
                    }
                }
                else
                {
                    var joinReq = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                            .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId AND {nameof(ClanRequestDto.PlayerId):C} = @PlayerId")
                            .WithParameters(new { ClubId = clan.Id, PlayerId = target }))
                        .FirstOrDefault();
                    if (joinReq == null)
                    {
                        outcome = ClubCommandResult.MemberNotFound;
                    }
                    else
                    {
                        if (cmd == ClubCommand.Accept)
                        {
                            var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                                    .Where($"{nameof(ClubDto.Id):C} = @ClubId")
                                    .WithParameters(new { ClubId = clan.Id }))
                                .FirstOrDefault();
                            var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                                    .Where($"{nameof(ClubPlayerDto.ClubId):C} = @ClubId")
                                    .WithParameters(new { ClubId = clan.Id }))
                                .ToList();
                            var capacity = Math.Max((clubRow?.Level ?? 1) * 12, 12);
                            if (clubRow == null || roster.Count >= capacity)
                            {
                                outcome = ClubCommandResult.MemberNotFound2;
                            }
                            else
                            {
                                DbUtil.Delete(gameDb, joinReq);
                                await clan.AddPlayer(target);
                                notifyDecision = true;
                                wasAccepted = true;
                            }
                        }
                        else if (cmd == ClubCommand.Decline)
                        {
                            DbUtil.Delete(gameDb, joinReq);
                            notifyDecision = true;
                            wasAccepted = false;
                        }
                        else
                        {
                            outcome = ClubCommandResult.PermissionDenied;
                        }
                    }
                }
            }
            await actor.SendAsync(new ClubAdminJoinCommandAckMessage((uint)outcome, target));
            if (outcome == ClubCommandResult.Success && notifyDecision)
                await SendClubJoinDecisionMessage(actor, clan, target, wasAccepted);
            await NotifyClubJoinWaitersChanged(clan.Id, $"AdminJoinCommand-{cmd}");
        }
        [MessageHandler(typeof(ClubAdminGradeChangeReqMessage))]
        public async Task ClubAdminGradeChangeReq(GameSession session, ClubAdminGradeChangeReqMessage message)
        {
            if (session?.Player == null || message == null || message.Changes == null || message.Changes.Length == 0 || message.Changes.Length > 64)
            {
                return;
            }
            var actor = session.Player;
            var clan = actor.Club;
            if (clan == null || clan.Id <= 0)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                return;
            }
            if (!clan.Players.TryGetValue(actor.Account.Id, out var senderSlot))
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                return;
            }
            if (senderSlot.Rank > ClubRank.Staff)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantReadClanInfo));
                return;
            }
            var rejects = new List<ulong>();
            foreach (var edit in message.Changes ?? Array.Empty<ClubAdminGradeChangeDto>())
            {
                if (!clan.Players.TryGetValue(edit.AccountId, out var targetSlot))
                {
                    rejects.Add(edit.AccountId);
                    continue;
                }
                if (targetSlot.Rank == ClubRank.Master)
                {
                    rejects.Add(edit.AccountId);
                    continue;
                }
                var desiredRank = (ClubRank)edit.Rank;
                if (desiredRank == ClubRank.None || desiredRank == ClubRank.Master || desiredRank > ClubRank.Cclass)
                {
                    rejects.Add(edit.AccountId);
                    continue;
                }
                targetSlot.Rank = desiredRank;
                using (var gameDb = GameDatabase.Open())
                {
                    await DbUtil.UpdateAsync(gameDb, new ClubPlayerDto
                    {
                        PlayerId = (int)targetSlot.AccountId,
                        ClubId = clan.Id,
                        Rank = (byte)desiredRank,
                        State = (int)ClubState.Joined
                    });
                }
                var targetNick = targetSlot.Account?.Nickname;
                if (string.IsNullOrWhiteSpace(targetNick))
                    targetNick = GetAccountNickname(targetSlot.AccountId);
                if (!string.IsNullOrWhiteSpace(targetNick))
                {
                    var delivered = await actor.Mailbox.SendTypedAsync(
                        targetNick,
                        "Rank Changed",
                        $"Your rank has been changed to {desiredRank}.",
                        2);
                }
            }
            await actor.SendAsync(new ClubAdminGradeChangeAckMessage
            {
                Unk = 0,
                Target = rejects.ToArray()
            });
            foreach (var onlineMember in GameServer.Instance.PlayerManager.Where(x => x.Club?.Id == clan.Id).ToArray())
            {
                try
                {
                    await onlineMember.SendAsync(new ClubMyInfoAckMessage(onlineMember.Map<Player, ClubMyInfoDto>()));
                }
                catch { }
                try
                {
                    if (onlineMember.ChatSession != null)
                    {
                        var memberDtos = clan.Players.Values
                            .Select(x => x.Map<ClubPlayerInfo, ClubMemberDto2>())
                            .ToArray();
                        await onlineMember.ChatSession.SendAsync(new ClubMemberListAck2Message(clan.Id, memberDtos));
                    }
                }
                catch { }
            }
        }
        [MessageHandler(typeof(ClubAdminInviteReqMessage))]
        public void ClubAdminInviteReq(GameSession session, ClubAdminInviteReqMessage message)
        {
            if (session?.Player == null || message == null || message.AccountId == 0)
            {
                session?.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            var inviter = session.Player;
            var clan = inviter.Club;
            var targetMember = GameServer.Instance.PlayerManager[message.AccountId];
            if (targetMember == null || inviter == null)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (clan == null || clan.Id <= 0 || !clan.Players.TryGetValue(inviter.Account.Id, out var senderSlot) || senderSlot.Rank > ClubRank.Staff)
            {
                return;
            }
            if ((targetMember.Club?.Id ?? 0) > 0 || IsPlayerInAnyClub(message.AccountId))
            {
                session.SendAsync(new ClubAdminInviteAckMessage((int)ClubMessage.AlreadyInClan));
                return;
            }
            if (inviter.Club.SendInvite(inviter, targetMember))
            {
                session.SendAsync(new ClubAdminInviteAckMessage(0));
            }
        }
        private static bool IsPlayerInAnyClub(ulong accountId)
        {
            using (var gameDb = GameDatabase.Open())
            {
                return DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                        .Where($"{nameof(ClubPlayerDto.PlayerId):C} = @AccountId")
                        .WithParameters(new { AccountId = accountId }))
                    .Any();
            }
        }
        [MessageHandler(typeof(ClubJoinReq2Message))]
        public async Task ClubJoinReq2(GameSession session, ClubJoinReq2Message message)
        {
            if (session?.Player == null || message == null || message.ClanId <= 0)
            {
                return;
            }
            var actor = session.Player;
            var ownClub = actor.Club;
            if (ownClub != null)
            {
                return;
            }
            var targetClub = GameServer.Instance.ClubManager.GetClub(message.ClanId);
            if (targetClub == null)
            {
                return;
            }
            var storedNewRequest = false;
            using (var gameDb = GameDatabase.Open())
            {
                var clubRow = DbUtil.Find<ClubDto>(gameDb, statement => statement
                 .Where($"{nameof(ClubDto.Id):C} = @{nameof(targetClub.Id)}")
                 .WithParameters(new { targetClub.Id })).FirstOrDefault();
                var roster = DbUtil.Find<ClubPlayerDto>(gameDb, statement => statement
                     .Where($"{nameof(ClubPlayerDto.ClubId):C} = @{nameof(targetClub.Id)}")
                     .WithParameters(new { targetClub.Id }))
                    .ToList();
                var existingReq = DbUtil.Find<ClanRequestDto>(gameDb, statement => statement
                     .Where($"{nameof(ClanRequestDto.ClubId):C} = @ClubId AND {nameof(ClanRequestDto.PlayerId):C} = @PlayerId")
                     .WithParameters(new { ClubId = targetClub.Id, PlayerId = (ulong)actor.Account.Id }))
                    .FirstOrDefault();
                if (clubRow == null || roster.Count >= clubRow.Level * 12)
                {
                    return;
                }
                string invitePattern = $"<Note Key =\"4\"Srl =\"{targetClub.Id}\"";
                Mail inviteMail = actor.Mailbox.FirstOrDefault((Mail x) => x.Message.StartsWith(invitePattern) && x.IsClan);
                if (inviteMail != null)
                {
                    await targetClub.AddPlayer(actor.Account.Id);
                    await actor.SendAsync(new ClubJoinAck2Message(0));
                    actor.Mailbox.Remove(new Mail[1] { inviteMail });
                    return;
                }
                if (existingReq != null)
                {
                    await actor.SendAsync(new ClubJoinAck2Message(0));
                    return;
                }
                var newReq = new ClanRequestDto
                {
                    ClubId = targetClub.Id,
                    PlayerId = (ulong)actor.Account.Id
                };
                await DbUtil.InsertAsync(gameDb, newReq);
                storedNewRequest = true;
                await actor.SendAsync(new ClubJoinAck2Message(0));
                var deliveries = 0;
                foreach (var staffMember in targetClub.Players.Values.Where(x => x.Rank <= ClubRank.Staff))
                {
                    var staffNick = staffMember.Account?.Nickname;
                    if (string.IsNullOrWhiteSpace(staffNick))
                        staffNick = GetAccountNickname(staffMember.AccountId);
                    if (string.IsNullOrWhiteSpace(staffNick))
                        continue;
                    var delivered = await actor.Mailbox.SendTypedAsync(
                        staffNick,
                        "Request to Join",
                        $"The player {actor.Account.Nickname} requested to join the club.",
                        2);
                    if (delivered)
                        deliveries++;
                }
            }
            if (storedNewRequest)
                await NotifyClubJoinWaitersChanged(targetClub.Id, "JoinReq2");
        }
        [MessageHandler(typeof(ClubSearchRoomReqMessage))]
        public void ClubSearchRoomReq(GameSession session, ClubSearchRoomReqMessage message) { }

        [MessageHandler(typeof(Match_Start_Req))]
        public void MatchStartReq(GameSession session, Match_Start_Req message) { }

        [MessageHandler(typeof(Match_Stop_Req))]
        public void MatchStopReq(GameSession session, Match_Stop_Req message) { }

        [MessageHandler(typeof(Match_List_Req))]
        public void MatchListReq(GameSession session, Match_List_Req message) { }

        [MessageHandler(typeof(Match_Invite_Req))]
        public void MatchInviteReq(GameSession session, Match_Invite_Req message) { }

        [MessageHandler(typeof(Battle_Invites_Received_Result))]
        public void BattleInvitesReceived(GameSession session, Battle_Invites_Received_Result message) { }

        [MessageHandler(typeof(ReMatchReqMessage))]
        public void ReMatchReq(GameSession session, ReMatchReqMessage message) { }

        [MessageHandler(typeof(MatchVoteBeginReqMessage))]
        public void MatchVoteBeginReq(GameSession session, MatchVoteBeginReqMessage message) { }

        [MessageHandler(typeof(MatchClubMarkReqMessage))]
        public void MatchClubMarkReq(GameSession session, MatchClubMarkReqMessage message) { }

        [MessageHandler(typeof(MatchPointReqMessage))]
        public void MatchPointReq(GameSession session, MatchPointReqMessage message) { }

        [MessageHandler(typeof(MatchRoomQuit_Req))]
        public void MatchRoomQuitReq(GameSession session, MatchRoomQuit_Req message) { }

        [MessageHandler(typeof(Club_Stadium_Edit_MapData_Req))]
        public void ClubStadiumEditMapData(GameSession session, Club_Stadium_Edit_MapData_Req message) { }

        [MessageHandler(typeof(Club_Stadium_Edit_Blastinfo_Edit_req))]
        public void ClubStadiumEditBlastinfo(GameSession session, Club_Stadium_Edit_Blastinfo_Edit_req message) { }

        [MessageHandler(typeof(Club_Stadium_Info_Req))]
        public void ClubStadiumInfoReq(GameSession session, Club_Stadium_Info_Req message) { }

        [MessageHandler(typeof(ClubNoticePointRefreshReqMessage))]
        public async Task ClubNoticePointRefreshReqMessage(GameSession session, ClubNoticePointRefreshReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var actor = session.Player;
            if (ShouldUseOtherClubNotice(actor))
            {
                TouchOtherClubVisit(actor);
                if (!TryResolveClubSnapshot(out var otherSnap, clubId: actor.ViewingOtherClubId))
                {
                    session.SendAsync(new ClubNotice_Point_Refresh_Ack
                    {
                        Unk1 = 0,
                        Unk2 = 0,
                        Unk3 = actor.Account.Id,
                        Unk4 = Array.Empty<int>()
                    });
                    return;
                }
                await session.SendAsync(new ClubOtherClubInfoAckMessage(0, BuildClubOtherClubInfo(otherSnap)));
                return;
            }
            if (actor.Club == null)
            {
                session.SendAsync(new ClubNotice_Point_Refresh_Ack
                {
                    Unk1 = 0,
                    Unk2 = 0,
                    Unk3 = actor.Account.Id,
                    Unk4 = Array.Empty<int>()
                });
                return;
            }
            if (!TryResolveClubSnapshot(out var ownSnap, clubId: actor.Club.Id))
            {
                session.SendAsync(new ClubNotice_Point_Refresh_Ack
                {
                    Unk1 = 0,
                    Unk2 = actor.Club.Id,
                    Unk3 = actor.Account.Id,
                    Unk4 = Array.Empty<int>()
                });
                return;
            }
            GameServer.Instance.ClubManager.UpdateClubWarStats(
                ownSnap.Dto.Id,
                ownSnap.Dto.Rank,
                ownSnap.Dto.Points,
                ownSnap.Dto.Win,
                ownSnap.Dto.Loss);
            var ownFight = GetClubFightStats(ownSnap);
            BeginOtherClubVisit(actor, ownSnap.Dto.Id);
            await session.SendAsync(new ClubOtherClubInfoAckMessage(0, BuildClubOtherClubInfo(ownSnap)));
        }
        [MessageHandler(typeof(ClubNoticeRecordRefreshReqMessage))]
        public async Task ClubNoticeRecordRefreshReqMessage(GameSession session, ClubNoticeRecordRefreshReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var actor = session.Player;
            if (ShouldUseOtherClubNotice(actor))
            {
                TouchOtherClubVisit(actor);
                if (!TryResolveClubSnapshot(out var otherSnap, clubId: actor.ViewingOtherClubId))
                {
                    session.SendAsync(new ClubNotice_Record_Refresh_Ack
                    {
                        Unk1 = 0,
                        Info = Array.Empty<ClubNoticeRecordDto>()
                    });
                    return;
                }
                await session.SendAsync(new ClubOtherClubInfoAckMessage(0, BuildClubOtherClubInfo(otherSnap)));
                await SendOtherClubRecordRefreshAsync(session, otherSnap);
                return;
            }
            var clan = actor.Club;
            var rows = Array.Empty<ClubNoticeRecordDto>();
            if (clan != null)
                rows = BuildClubNoticeRecords(clan.Id, clan.ClanName, (int)clan.ClubWin, (int)clan.ClubLoss);
            session.SendAsync(new ClubNotice_Record_Refresh_Ack
            {
                Unk1 = 0,
                Info = rows
            });
        }
        [MessageHandler(typeof(ClubOtherClubinfoReqMessage))]
        public async Task ClubOtherClubinfoReq(GameSession session, ClubOtherClubinfoReqMessage message)
        {
            if (session?.Player == null || message == null || string.IsNullOrWhiteSpace(message.ClubName) || message.ClubName.Length > 32)
            {
                session?.SendAsync(new ClubOtherClubInfoAckMessage(1, new ClubOtherClubInfo()));
                return;
            }
            var actor = session.Player;
            if (!TryResolveClubSnapshot(out var snap, clubName: message.ClubName))
            {
                await session.SendAsync(new ClubOtherClubInfoAckMessage(1, new ClubOtherClubInfo()));
                return;
            }
            BeginOtherClubVisit(actor, snap.Dto.Id);
            var info = BuildClubOtherClubInfo(snap);
            var fight = GetClubFightStats(snap);
            var viewingOwn = actor.Club != null && snap.Dto.Id == actor.Club.Id;
            await session.SendAsync(new ClubOtherClubInfoAckMessage(0, info));
            await SendOtherClubRecordRefreshAsync(session, snap);
        }
    }
}

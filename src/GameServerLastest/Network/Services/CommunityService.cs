using System;
using System.Linq;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using ExpressMapper.Extensions;
using Santana.Network.Data.Chat;
using Santana.Network.Data.Game;
using Santana.Network.Message.Chat;
using Santana.Database.Auth;
using Santana.Database.Game;
using ProudNetSrc.Handlers;
using Dapper;
using Dapper.FastCrud;
using Santana.Network.Message.Game;
using Serilog;
using Serilog.Core;
using MySqlConnector;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Santana.Network.Services
{
    internal class CommunityService : ProudMessageHandler
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(CommunityService));

        private static readonly ConcurrentDictionary<string, DateTime> _inviteGrants =
            new ConcurrentDictionary<string, DateTime>();

        private const int ErrTableMissing = 1146;
        private const int CombiTextCap = 32;
        private const string CombiFillerSlot = "CampoCombiNose";

        private const int AckAdd = 0;
        private const int AckDelete = 1;
        private const int AckAccept = 2;
        private const int AckDeny = 3;

        private const uint WireRequesting = 1;
        private const uint WireAccepted = 2;
        private const uint WireInbox = 3;

        internal static void AllowRoomInviteEntry(Player player, Room room)
        {
            if (player?.Account == null || room == null)
                return;

            _inviteGrants[$"{player.Account.Id}:{room.Id}"] = DateTime.UtcNow.AddSeconds(30);
        }

        internal static bool ConsumeRoomInviteEntry(Player player, Room room)
        {
            if (player?.Account == null || room == null)
                return false;

            var grantKey = $"{player.Account.Id}:{room.Id}";
            if (!_inviteGrants.TryRemove(grantKey, out var deadline))
                return false;

            return deadline >= DateTime.UtcNow;
        }

        private static bool IsCombiTableMissing(Exception error)
        {
            if (error is MySqlException sqlError)
                return sqlError.Number == ErrTableMissing &&
                       sqlError.Message.IndexOf(".combi", StringComparison.OrdinalIgnoreCase) >= 0;

            return error.InnerException != null && IsCombiTableMissing(error.InnerException);
        }

        private static void WarnCombiTableMissing(Exception error)
        {
            _log.Warning(error,
                "[Combi] Table 'combi' missing in configured Game DB. Import/create combi table or set GameServer.hjson database.game.database to the DB that owns it.");
        }

        private static string NormalizeCombiText(string raw)
        {
            raw = (raw ?? "").Trim();
            raw = new string(raw.Where(ch => !char.IsControl(ch)).ToArray());
            return raw.Length > CombiTextCap ? raw.Substring(0, CombiTextCap) : raw;
        }

        private static bool RawCombiTextRejected(string raw)
        {
            if (raw == null)
                return false;

            return raw.Length > CombiTextCap || raw.Any(char.IsControl);
        }

        private static bool FitsSqlInt(ulong source, out int narrowed)
        {
            if (source > int.MaxValue)
            {
                narrowed = 0;
                return false;
            }

            narrowed = (int)source;
            return true;
        }

        private static bool FitsSqlLong(ulong source, out long narrowed)
        {
            if (source > long.MaxValue)
            {
                narrowed = 0;
                return false;
            }

            narrowed = (long)source;
            return true;
        }

        private static async Task PushCombiFailAsync(Player who, ulong targetValue, string combiTitle, string mateNick, string stamp, int slot = 0)
        {
            if (who?.Account == null)
                return;

            var failDto = BuildCombiDto(
                targetValue,
                (ulong)who.Account.Id,
                0,
                0, 0, 0,
                0, 0, 0,
                NormalizeCombiText(combiTitle),
                NormalizeCombiText(mateNick),
                stamp
            );

            await who.SendAsync(new CombiActionAckMessage(1, slot, failDto));
        }

        private static async Task PushCombiPendingAsync(Player who, CombiDto entry, bool incoming)
        {
            if (who?.ChatSession == null)
                return;

            await who.ChatSession.SendAsync(new CombiActionAckMessage(0, AckAdd, entry));
        }

        [MessageHandler(typeof(OptionSaveCommunityReqMessage))]
        public void OptionSaveCommunityReq(ChatSession session, OptionSaveCommunityReqMessage message)
        {
            var who = session.Player;

            who.Settings.AddOrUpdate("AllowCombiInvite", message.AllowCombi);
            who.Settings.AddOrUpdate("AllowFriendRequest", message.AllowFriendReq);
            who.Settings.AddOrUpdate("AllowRoomInvite", message.AllowInvite);
            who.Settings.AddOrUpdate("AllowInfoRequest", message.RevealInfo);
        }

        [MessageHandler(typeof(OptionSaveBinaryReqMessage))]
        public void OptionSaveBinaryReq(ChatSession session, OptionSaveBinaryReqMessage message)
        {
        }

        [MessageHandler(typeof(UserDataOneReqMessage))]
        public void GetUserDataHandler(ChatSession session, UserDataOneReqMessage message)
        {
            var who = session.Player;
            var subject = GameServer.Instance.PlayerManager[message.AccountId];

            if (who == null || message.AccountId == who.Account.Id || subject == null)
                return;

            switch (subject.Settings.GetSetting("AllowInfoRequest"))
            {
                case CommunitySetting.Deny:
                    return;

                case CommunitySetting.FriendOnly:
                    who.FriendManager.GetValue(subject.Account.Id, out Friend link);
                    if (link == null)
                        return;
                    break;
            }

            session.SendAsync(new UserDataFourAckMessage(25, subject.Map<Player, UserDataDto>()));
        }

        private static string LookupNickname(ulong accountId)
        {
            try
            {
                using (var authdb = AuthDatabase.Open())
                {
                    var record = DbUtil.Find<AccountDto>(authdb, statement => statement
                        .Where($"{nameof(AccountDto.Id):C} = @Id")
                        .WithParameters(new { Id = (int)accountId }))
                        .FirstOrDefault();

                    return record?.Nickname ?? "";
                }
            }
            catch
            {
                return "";
            }
        }

        private static AccountDto ResolveAccount(ulong accountId, string nameOrLogin)
        {
            try
            {
                using (var authdb = AuthDatabase.Open())
                {
                    if (FitsSqlInt(accountId, out var numericId) && numericId > 0)
                    {
                        var byId = DbUtil.Find<AccountDto>(authdb, statement => statement
                            .Where($"{nameof(AccountDto.Id):C} = @Id")
                            .WithParameters(new { Id = numericId }))
                            .FirstOrDefault();

                        if (byId != null)
                            return byId;
                    }

                    var cleaned = NormalizeCombiText(nameOrLogin);
                    if (string.IsNullOrWhiteSpace(cleaned))
                        return null;

                    return DbUtil.Find<AccountDto>(authdb, statement => statement
                        .Where($"{nameof(AccountDto.Nickname):C} = @Name OR {nameof(AccountDto.Username):C} = @Name")
                        .WithParameters(new { Name = cleaned }))
                        .FirstOrDefault();
                }
            }
            catch (Exception error)
            {
                _log.Warning(error, "[Combi] Account lookup failed: id={AccountId}, name={Name}", accountId, nameOrLogin);
                return null;
            }
        }

        private static Player ResolveOnlinePlayer(ulong accountId, string nameOrLogin)
        {
            return GameServer.Instance.PlayerManager.FirstOrDefault(candidate =>
                candidate?.Account != null &&
                (
                    (accountId != 0 && (ulong)candidate.Account.Id == accountId) ||
                    string.Equals(candidate.Account.Nickname, nameOrLogin, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Account.Username, nameOrLogin, StringComparison.OrdinalIgnoreCase)
                ));
        }

        private static Player ResolveInviteTarget(ulong rawId)
        {
            var direct = GameServer.Instance.PlayerManager[rawId];
            if (direct != null)
                return direct;

            var lowHalf = (ulong)(uint)rawId;
            if (lowHalf != rawId)
            {
                var byLow = GameServer.Instance.PlayerManager[lowHalf];
                if (byLow != null)
                    return byLow;
            }

            var highHalf = rawId >> 32;
            if (highHalf != 0 && highHalf != rawId)
            {
                var byHigh = GameServer.Instance.PlayerManager[highHalf];
                if (byHigh != null)
                    return byHigh;
            }

            return null;
        }

        public static async Task<PlayerInfoDto[]> GetCombiPlayerInfos(Player plr)
        {
            if (plr?.Account == null)
                return Array.Empty<PlayerInfoDto>();

            try
            {
                using (var db = GameDatabase.Open())
                {
                    var pairs = (await db.QueryAsync(
                        @"SELECT PlayerId, CombiPlayerId
                          FROM combi
                          WHERE (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)
                            AND (State = 0 OR State = 1)",
                        new { PlayerId = (int)plr.Account.Id }))
                        .ToArray();

                    var selfId = (ulong)plr.Account.Id;
                    var mateIds = pairs
                        .Select(pair =>
                        {
                            var left = Convert.ToUInt64(pair.PlayerId);
                            var right = Convert.ToUInt64(pair.CombiPlayerId);
                            return left == selfId ? right : left;
                        })
                        .Where(id => id > 0 && id != selfId)
                        .Distinct()
                        .ToArray();

                    if (mateIds.Length == 0)
                        return Array.Empty<PlayerInfoDto>();

                    using (var authdb = AuthDatabase.Open())
                    {
                        return mateIds
                            .Select(id => BuildPlayerInfo((ulong)id, authdb, db))
                            .Where(info => info.Info.AccountId > 0)
                            .ToArray();
                    }
                }
            }
            catch (Exception error) when (IsCombiTableMissing(error))
            {
                WarnCombiTableMissing(error);
                return Array.Empty<PlayerInfoDto>();
            }
            catch (Exception error)
            {
                _log.Warning(error, "[Combi] Failed to build player info list for player {PlayerId}", plr.Account.Id);
                return Array.Empty<PlayerInfoDto>();
            }
        }

        public static async Task SendCommunityPlayerInfoList(Player plr)
        {
            if (plr?.ChatSession == null)
                return;

            var roster = plr.FriendManager.Select(f => f.Map<Friend, PlayerInfoDto>()).ToList();

            foreach (var combiInfo in await GetCombiPlayerInfos(plr))
            {
                if (roster.All(existing => existing.Info.AccountId != combiInfo.Info.AccountId))
                    roster.Add(combiInfo);
            }

            if (plr.Club?.Id > 0)
            {
                foreach (var clubInfo in plr.Club.Players.Select(entry => entry.Value.Map<ClubPlayerInfo, PlayerInfoDto>()))
                {
                    if (roster.All(existing => existing.Info.AccountId != clubInfo.Info.AccountId))
                        roster.Add(clubInfo);
                }
            }

            await plr.ChatSession.SendAsync(new ChatPlayerInfoListAckMessage(roster.ToArray()));
        }

        private static PlayerInfoDto BuildPlayerInfo(ulong accountId, System.Data.IDbConnection authdb, System.Data.IDbConnection gamedb)
        {
            var live = GameServer.Instance.PlayerManager.Get(accountId);
            if (live != null)
                return live.Map<Player, PlayerInfoDto>();

            var account = DbUtil.Find<AccountDto>(authdb, statement => statement
                .Where($"{nameof(AccountDto.Id):C} = @Id")
                .WithParameters(new { Id = accountId }))
                .FirstOrDefault();

            if (account == null)
                return new PlayerInfoDto();

            var stats = DbUtil.Find<PlayerDto>(gamedb, statement => statement
                .Where($"{nameof(PlayerDto.Id):C} = @Id")
                .WithParameters(new { Id = accountId }))
                .FirstOrDefault();

            var shownName = !string.IsNullOrWhiteSpace(account.Nickname)
                ? account.Nickname
                : account.Username ?? string.Empty;

            return new PlayerInfoDto(
                new PlayerInfoShortDto(
                    accountId,
                    shownName,
                    stats?.TotalExperience ?? 0,
                    (SecurityLevel)account.SecurityLevel >= SecurityLevel.GameSage),
                new PlayerLocationDto());
        }

        private static CombiDto BuildCombiDto(
            ulong rowId,
            ulong viewerId,
            ulong mateId,
            uint exp,
            uint battle,
            uint match,
            ulong win,
            ulong defeat,
            ulong wireState,
            string combiTitle,
            string mateNick,
            string stamp)
        {
            return new CombiDto
            {
                Unk1 = mateId,
                Unk2 = (uint)wireState,
                Unk3 = (uint)wireState,
                Unk4 = match,

                Unk5 = exp,
                Unk6 = mateId,

                Unk7 = battle,
                Unk8 = win,
                Unk9 = defeat,

                Unk10 = CombiFillerSlot,
                Unk11 = mateNick ?? "",
                Unk12 = combiTitle ?? "",
                Unk13 = stamp ?? ""
            };
        }

        [MessageHandler(typeof(CombiCheckNameReqMessage))]
        public async Task CombiCheckNameReqMessage(ChatSession session, CombiCheckNameReqMessage message)
        {
            var who = session.Player;
            var wanted = NormalizeCombiText(message.Name);

            if (who == null)
                return;

            who.HeartBeat = DateTime.Now.AddMinutes(1);

            if (string.IsNullOrWhiteSpace(wanted) || RawCombiTextRejected(message.Name))
            {
                _log.Warning("[Combi] CheckName rejected: player={PlayerId}, nameLen={Length}",
                    who.Account?.Id, message.Name?.Length ?? 0);
                await who.SendAsync(new CombiCheckNameAckMessage(100, wanted));
                return;
            }

            try
            {
                using (var db = GameDatabase.Open())
                {
                    var alreadyUsed = await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM combi WHERE CombiName = @Name",
                        new { Name = wanted });

                    if (alreadyUsed > 0)
                    {
                        await who.SendAsync(new CombiCheckNameAckMessage(100, wanted));
                        return;
                    }
                }
            }
            catch (Exception error) when (IsCombiTableMissing(error))
            {
                WarnCombiTableMissing(error);
                await who.SendAsync(new CombiCheckNameAckMessage(100, wanted));
                return;
            }

            await who.SendAsync(new CombiCheckNameAckMessage(0, wanted));
        }

        public static async Task SendCombiList(Player plr)
        {
            if (plr?.Account == null)
                return;

            try
            {
                using (var db = GameDatabase.Open())
                {
                    var rows = (await db.QueryAsync(
                        @"SELECT
                    Id,
                    PlayerId,
                    CombiPlayerId,
                    Exp,
                    Battle,
                    `Match` AS MatchCount,
                    Win,
                    Defeat,
                    CombiName,
                    CombiMate,
                    CombiDate,
                    State
                  FROM combi
                  WHERE (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)
                    AND (State = 1 OR State = 0)",
                        new { PlayerId = (int)plr.Account.Id }))
                        .ToArray();

                    if (rows.Length == 0)
                    {
                        if (plr.ChatSession != null)
                            await plr.ChatSession.SendAsync(new CombiListAckMessage(Array.Empty<CombiDto>()));

                        return;
                    }

                    var selfId = (ulong)plr.Account.Id;

                    var entries = rows.Select(row =>
                    {
                        var ownerId = Convert.ToUInt64(row.PlayerId);
                        var mateId = Convert.ToUInt64(row.CombiPlayerId);
                        var iAmOwner = selfId == ownerId;

                        var storedName = Convert.ToString(row.CombiName) ?? "";
                        var storedMate = Convert.ToString(row.CombiMate) ?? "";
                        var storedDate = Convert.ToString(row.CombiDate) ?? "";

                        string nickShown;
                        if (iAmOwner)
                        {
                            nickShown = storedMate;
                        }
                        else
                        {
                            var ownerLive = GameServer.Instance.PlayerManager
                                .FirstOrDefault(c => c?.Account != null && (ulong)c.Account.Id == ownerId);

                            nickShown = ownerLive?.Account?.Nickname ?? LookupNickname(ownerId);
                        }

                        if (string.IsNullOrWhiteSpace(nickShown))
                            nickShown = "Unknown";

                        var storedState = Convert.ToUInt32(row.State);
                        var wireState = storedState == 1 ? WireAccepted : WireRequesting;

                        return new CombiDto
                        {
                            Unk1 = iAmOwner ? mateId : ownerId,
                            Unk2 = wireState,
                            Unk3 = wireState,
                            Unk4 = Convert.ToUInt32(row.MatchCount),

                            Unk5 = Convert.ToUInt64(row.Exp),
                            Unk6 = iAmOwner ? mateId : ownerId,

                            Unk7 = Convert.ToUInt64(row.Battle),
                            Unk8 = Convert.ToUInt64(row.Win),
                            Unk9 = Convert.ToUInt64(row.Defeat),

                            Unk10 = CombiFillerSlot,
                            Unk11 = nickShown,
                            Unk12 = storedName,
                            Unk13 = storedDate
                        };
                    }).ToArray();

                    if (plr.ChatSession != null)
                    {
                        _log.Information("[Combi] List sent: player={PlayerId}, rows={Rows}, states={States}",
                            plr.Account.Id,
                            entries.Length,
                            string.Join(",", entries.Select(x => $"{x.Unk1}:{x.Unk2}/{x.Unk3}:{x.Unk11}:{x.Unk12}")));
                        await PushCombiMasterInfo(plr, "COMBI.LIST.BEFORE");
                        await plr.ChatSession.SendAsync(new CombiListAckMessage(entries));
                        await PushCombiPresence(plr, entries.Select(x => x.Unk6).ToArray());
                        await PushCombiMasterInfo(plr, "COMBI.LIST.AFTER");
                    }
                }
            }
            catch (Exception error) when (IsCombiTableMissing(error))
            {
                WarnCombiTableMissing(error);

                if (plr.ChatSession != null)
                    await plr.ChatSession.SendAsync(new CombiListAckMessage(Array.Empty<CombiDto>()));
            }
            catch (Exception error)
            {
                _log.Warning(error, "[Combi] Failed to send combi list for player {PlayerId}", plr.Account.Id);
            }
        }

        private static async Task PushCombiMasterInfo(Player plr, string origin)
        {
            if (plr?.Session == null)
                return;

            var master = plr.Map<Player, PlayerAccountInfoDto>();
            await plr.SendAsync(new PlayerAccountInfoAckMessage(master));
            Console.WriteLine($"[COMBI MASTER] SOURCE={origin} PLAYER={plr.Account.Id} LEVEL={master.Level} EXP={master.CombiMasterExp} TOP_EXP_GATE={(master.Level >= 25 ? 1 : 0)}");
        }

        private static Task PushCombiPresence(Player plr, ulong[] mateIds)
        {
            if (plr?.ChatSession == null || mateIds == null || mateIds.Length == 0)
                return Task.CompletedTask;

            var pushed = 0;
            foreach (var mateId in mateIds.Distinct())
            {
                if (mateId == 0 || mateId == plr.Account.Id)
                    continue;

                var mateLive = GameServer.Instance.PlayerManager.Get(mateId);
                if (mateLive == null)
                    continue;

                Club.SendLivePresence(plr, mateLive, "COMBI.SNAPSHOT");
                pushed++;
            }

            Console.WriteLine($"[COMBI PRESENCE] VIEWER={plr.Account.Id} LIVE_PARTNERS={pushed}");
            return Task.CompletedTask;
        }

        public static async Task SendPendingCombiRequests(Player plr)
        {
            if (plr?.Account == null)
                return;

            try
            {
                using (var db = GameDatabase.Open())
                {
                    var rows = (await db.QueryAsync(
                        @"SELECT
                    Id,
                    PlayerId,
                    CombiPlayerId,
                    Exp,
                    Battle,
                    `Match` AS MatchCount,
                    Win,
                    Defeat,
                    CombiName,
                    CombiMate,
                    CombiDate,
                    State
                  FROM combi
                  WHERE CombiPlayerId = @PlayerId
                    AND State = 0",
                        new { PlayerId = (int)plr.Account.Id }))
                        .ToArray();

                    foreach (var row in rows)
                    {
                        var rowId = Convert.ToUInt64(row.Id);
                        var ownerId = Convert.ToUInt64(row.PlayerId);
                        var storedName = Convert.ToString(row.CombiName) ?? "";
                        var storedDate = Convert.ToString(row.CombiDate) ?? "";
                        var ownerNick = LookupNickname(ownerId);

                        if (string.IsNullOrWhiteSpace(ownerNick))
                            ownerNick = "Unknown";

                        var pending = BuildCombiDto(
                            rowId,
                            (ulong)plr.Account.Id,
                            ownerId,
                            Convert.ToUInt32(row.Exp),
                            Convert.ToUInt32(row.Battle),
                            Convert.ToUInt32(row.MatchCount),
                            Convert.ToUInt64(row.Win),
                            Convert.ToUInt64(row.Defeat),
                            WireInbox,
                            storedName,
                            ownerNick,
                            storedDate
                        );

                        await PushCombiPendingAsync(plr, pending, true);
                    }

                    if (rows.Length > 0)
                        _log.Information("[Combi] Pending requests sent: player={PlayerId}, count={Count}",
                            plr.Account.Id, rows.Length);
                }
            }
            catch (Exception error) when (IsCombiTableMissing(error))
            {
                WarnCombiTableMissing(error);
            }
            catch (Exception error)
            {
                _log.Warning(error, "[Combi] Failed to send pending requests for player {PlayerId}", plr.Account.Id);
            }
        }

        [MessageHandler(typeof(CombiActionReqMessage))]
        public async Task CombiActionReq(ChatSession session, CombiActionReqMessage message)
        {
            var who = session.Player;

            if (who?.Account == null)
                return;

            var verb = message.Action;
            var targetValue = message.TargetAccountId;

            var mateNick = NormalizeCombiText(message.CombiName);
            var combiTitle = NormalizeCombiText(message.CombiMate);
            var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");

            if (verb > 3 ||
                RawCombiTextRejected(message.CombiName) ||
                RawCombiTextRejected(message.CombiMate))
            {
                _log.Warning("[Combi] Action rejected: player={PlayerId}, action={Action}, target={Target}, nameLen={NameLen}, mateLen={MateLen}",
                    who.Account.Id, verb, targetValue, message.CombiName?.Length ?? 0, message.CombiMate?.Length ?? 0);
                await PushCombiFailAsync(who, targetValue, combiTitle, mateNick, stamp);
                return;
            }

            try
            {
                if (verb == 1)
                {
                    if (!FitsSqlLong(targetValue, out var combiRowId))
                    {
                        _log.Warning("[Combi] Delete rejected: player={PlayerId}, bad row id={Target}", who.Account.Id, targetValue);
                        await PushCombiFailAsync(who, targetValue, combiTitle, mateNick, stamp);
                        return;
                    }

                    using (var db = GameDatabase.Open())
                    {
                        var row = await db.QueryFirstOrDefaultAsync(
                            @"SELECT Id, PlayerId, CombiPlayerId, CombiName, CombiMate, CombiDate
                      FROM combi
                      WHERE ((Id = @CombiId)
                         OR (PlayerId = @PlayerId AND CombiPlayerId = @TargetAccountId)
                         OR (PlayerId = @TargetAccountId AND CombiPlayerId = @PlayerId))
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)
                      LIMIT 1",
                            new
                            {
                                CombiId = combiRowId,
                                PlayerId = (int)who.Account.Id,
                                TargetAccountId = (long)targetValue
                            });

                        if (row == null)
                        {
                            await SendCombiList(who);
                            return;
                        }

                        var rowId = Convert.ToUInt64(row.Id);
                        var ownerId = Convert.ToUInt64(row.PlayerId);
                        var mateId = Convert.ToUInt64(row.CombiPlayerId);
                        var otherId = ownerId == (ulong)who.Account.Id ? mateId : ownerId;

                        var storedName = Convert.ToString(row.CombiName) ?? "";
                        var storedDate = Convert.ToString(row.CombiDate) ?? "";
                        var otherNick = LookupNickname(otherId);

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = mateNick;

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = "Unknown";

                        await db.ExecuteAsync(
                            @"DELETE FROM combi
                      WHERE Id = @CombiId
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                            new
                            {
                                CombiId = (long)rowId,
                                PlayerId = (int)who.Account.Id
                            });

                        var mine = BuildCombiDto(
                            rowId,
                            (ulong)who.Account.Id,
                            otherId,
                            0, 0, 0,
                            0, 0, WireAccepted,
                            storedName,
                            otherNick,
                            storedDate
                        );

                        await who.SendAsync(new CombiActionAckMessage(0, AckDelete, mine));
                        await SendCombiList(who);

                        var otherLive = ResolveOnlinePlayer(otherId, "");
                        if (otherLive != null)
                        {
                            var theirs = BuildCombiDto(
                                rowId,
                                otherId,
                                (ulong)who.Account.Id,
                                0, 0, 0,
                                0, 0, WireAccepted,
                                storedName,
                                who.Account.Nickname ?? "",
                                storedDate
                            );

                            await otherLive.SendAsync(new CombiActionAckMessage(0, AckDelete, theirs));
                            await SendCombiList(otherLive);
                        }
                    }

                    return;
                }

                if (verb == 2)
                {
                    if (!FitsSqlLong(targetValue, out var combiRowId) ||
                        !FitsSqlInt(targetValue, out var targetAccountId))
                    {
                        _log.Warning("[Combi] Accept rejected: player={PlayerId}, bad target={Target}", who.Account.Id, targetValue);
                        await PushCombiFailAsync(who, targetValue, combiTitle, mateNick, stamp, 2);
                        return;
                    }

                    using (var db = GameDatabase.Open())
                    {
                        var row = await db.QueryFirstOrDefaultAsync(
                            @"SELECT Id, PlayerId, CombiPlayerId, CombiName, CombiMate, CombiDate
                      FROM combi
                      WHERE (Id = @CombiId OR PlayerId = @TargetId OR CombiPlayerId = @TargetId)
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)
                      LIMIT 1",
                            new
                            {
                                CombiId = combiRowId,
                                TargetId = targetAccountId,
                                PlayerId = (int)who.Account.Id
                            });

                        if (row == null)
                        {
                            var failDto = BuildCombiDto(
                                targetValue,
                                (ulong)who.Account.Id,
                                0,
                                0, 0, 0,
                                0, 0, 0,
                                combiTitle,
                                mateNick,
                                stamp
                            );

                            await who.SendAsync(new CombiActionAckMessage(1, 2, failDto));
                            await SendCombiList(who);
                            return;
                        }

                        var rowId = Convert.ToUInt64(row.Id);
                        var ownerId = Convert.ToUInt64(row.PlayerId);
                        var mateId = Convert.ToUInt64(row.CombiPlayerId);
                        var otherId = ownerId == (ulong)who.Account.Id ? mateId : ownerId;

                        var storedName = Convert.ToString(row.CombiName) ?? "";
                        var storedDate = Convert.ToString(row.CombiDate) ?? "";
                        var otherNick = LookupNickname(otherId);

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = mateNick;

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = "Unknown";

                        await db.ExecuteAsync(
                            @"UPDATE combi
                      SET State = 1
                      WHERE Id = @CombiId
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                            new
                            {
                                CombiId = (long)rowId,
                                PlayerId = (int)who.Account.Id
                            });

                        var mine = BuildCombiDto(
                            rowId,
                            (ulong)who.Account.Id,
                            otherId,
                            0, 0, 0,
                            0, 0, WireAccepted,
                            storedName,
                            otherNick,
                            storedDate
                        );

                        await who.SendAsync(new CombiActionAckMessage(0, AckAccept, mine));
                        await SendCombiList(who);

                        var otherLive = ResolveOnlinePlayer(otherId, "");
                        if (otherLive != null)
                        {
                            var theirs = BuildCombiDto(
                                rowId,
                                otherId,
                                (ulong)who.Account.Id,
                                0, 0, 0,
                                0, 0, WireAccepted,
                                storedName,
                                who.Account.Nickname ?? "",
                                storedDate
                            );

                            await otherLive.SendAsync(new CombiActionAckMessage(0, AckAccept, theirs));
                            await SendCombiList(otherLive);
                        }
                    }

                    return;
                }

                if (verb == 3)
                {
                    if (!FitsSqlLong(targetValue, out var combiRowId) ||
                        !FitsSqlInt(targetValue, out var targetAccountId))
                    {
                        _log.Warning("[Combi] Deny rejected: player={PlayerId}, bad target={Target}", who.Account.Id, targetValue);
                        await PushCombiFailAsync(who, targetValue, combiTitle, mateNick, stamp);
                        return;
                    }

                    using (var db = GameDatabase.Open())
                    {
                        var row = await db.QueryFirstOrDefaultAsync(
                            @"SELECT Id, PlayerId, CombiPlayerId, CombiName, CombiMate, CombiDate
                      FROM combi
                      WHERE (Id = @CombiId OR PlayerId = @TargetId OR CombiPlayerId = @TargetId)
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)
                      LIMIT 1",
                            new
                            {
                                CombiId = combiRowId,
                                TargetId = targetAccountId,
                                PlayerId = (int)who.Account.Id
                            });

                        if (row == null)
                        {
                            await SendCombiList(who);
                            return;
                        }

                        var rowId = Convert.ToUInt64(row.Id);
                        var ownerId = Convert.ToUInt64(row.PlayerId);
                        var mateId = Convert.ToUInt64(row.CombiPlayerId);
                        var otherId = ownerId == (ulong)who.Account.Id ? mateId : ownerId;

                        var storedName = Convert.ToString(row.CombiName) ?? "";
                        var storedDate = Convert.ToString(row.CombiDate) ?? "";
                        var otherNick = LookupNickname(otherId);

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = mateNick;

                        if (string.IsNullOrWhiteSpace(otherNick))
                            otherNick = "Unknown";

                        await db.ExecuteAsync(
                            @"DELETE FROM combi
                      WHERE Id = @CombiId
                        AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                            new
                            {
                                CombiId = (long)rowId,
                                PlayerId = (int)who.Account.Id
                            });

                        var mine = BuildCombiDto(
                            rowId,
                            (ulong)who.Account.Id,
                            otherId,
                            0, 0, 0,
                            0, 0, 0,
                            storedName,
                            otherNick,
                            storedDate
                        );

                        await who.SendAsync(new CombiActionAckMessage(0, AckDeny, mine));
                        await SendCombiList(who);

                        var otherLive = ResolveOnlinePlayer(otherId, "");
                        if (otherLive != null)
                        {
                            var theirs = BuildCombiDto(
                                rowId,
                                otherId,
                                (ulong)who.Account.Id,
                                0, 0, 0,
                                0, 0, 0,
                                storedName,
                                who.Account.Nickname ?? "",
                                storedDate
                            );

                            await otherLive.SendAsync(new CombiActionAckMessage(0, AckDeny, theirs));
                            await SendCombiList(otherLive);
                        }
                    }

                    return;
                }

                if (verb != 0)
                {
                    var failDto = BuildCombiDto(
                        targetValue,
                        (ulong)who.Account.Id,
                        0,
                        0, 0, 0,
                        0, 0, 0,
                        combiTitle,
                        mateNick,
                        stamp
                    );

                    await who.SendAsync(new CombiActionAckMessage(1, 0, failDto));
                    return;
                }

                if (string.IsNullOrWhiteSpace(combiTitle))
                {
                    var failDto = BuildCombiDto(
                        targetValue,
                        (ulong)who.Account.Id,
                        0,
                        0, 0, 0,
                        0, 0, 0,
                        combiTitle,
                        mateNick,
                        stamp
                    );

                    await who.SendAsync(new CombiActionAckMessage(1, 0, failDto));
                    return;
                }

                var targetAccount = ResolveAccount(targetValue, mateNick);
                var targetLive = ResolveOnlinePlayer(
                    targetAccount != null ? (ulong)targetAccount.Id : targetValue,
                    mateNick);

                if (targetAccount == null)
                {
                    _log.Warning("[Combi] Add rejected: target not found. player={PlayerId}, targetId={TargetId}, targetName={TargetName}",
                        who.Account.Id, targetValue, mateNick);

                    var missingDto = BuildCombiDto(
                        targetValue,
                        (ulong)who.Account.Id,
                        0,
                        0, 0, 0,
                        0, 0, 0,
                        combiTitle,
                        mateNick,
                        stamp
                    );

                    await who.SendAsync(new CombiActionAckMessage(1, 1, missingDto));
                    return;
                }

                targetValue = (ulong)targetAccount.Id;
                mateNick = targetAccount.Nickname ?? targetAccount.Username ?? mateNick;

                if (targetValue == 0 ||
                    targetValue == (ulong)who.Account.Id ||
                    !FitsSqlInt(targetValue, out var targetPlayerId))
                {
                    var missingDto = BuildCombiDto(
                        targetValue,
                        (ulong)who.Account.Id,
                        0,
                        0, 0, 0,
                        0, 0, 0,
                        combiTitle,
                        mateNick,
                        stamp
                    );

                    await who.SendAsync(new CombiActionAckMessage(1, 1, missingDto));
                    return;
                }

                long freshCombiId;

                using (var db = GameDatabase.Open())
                {
                    var nameTaken = await db.ExecuteScalarAsync<int>(
                        "SELECT COUNT(*) FROM combi WHERE CombiName = @CombiName",
                        new { CombiName = combiTitle });

                    var pairTaken = await db.ExecuteScalarAsync<int>(
                        @"SELECT COUNT(*) FROM combi
                  WHERE (PlayerId = @PlayerId AND CombiPlayerId = @TargetId)
                     OR (PlayerId = @TargetId AND CombiPlayerId = @PlayerId)",
                        new
                        {
                            PlayerId = (int)who.Account.Id,
                            TargetId = targetPlayerId
                        });

                    if (nameTaken > 0 || pairTaken > 0)
                    {
                        var failDto = BuildCombiDto(
                            targetValue,
                            (ulong)who.Account.Id,
                            targetValue,
                            0, 0, 0,
                            0, 0, 0,
                            combiTitle,
                            mateNick,
                            stamp
                        );

                        await who.SendAsync(new CombiActionAckMessage(1, 0, failDto));
                        return;
                    }

                    await db.ExecuteAsync(
                        @"INSERT INTO combi
                  (PlayerId, CombiPlayerId, Exp, Battle, `Match`, Win, Defeat, CombiName, CombiMate, CombiDate, State)
                  VALUES
                  (@PlayerId, @CombiPlayerId, 0, 0, 0, 0, 0, @CombiName, @CombiMate, @CombiDate, 0)",
                        new
                        {
                            PlayerId = (int)who.Account.Id,
                            CombiPlayerId = targetPlayerId,
                            CombiName = combiTitle,
                            CombiMate = mateNick,
                            CombiDate = stamp
                        });

                    freshCombiId = await db.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");
                }

                var senderCombi = BuildCombiDto(
                    (ulong)freshCombiId,
                    (ulong)who.Account.Id,
                    targetValue,
                    0, 0, 0,
                    0, 0, WireRequesting,
                    combiTitle,
                    mateNick,
                    stamp
                );

                var targetCombi = BuildCombiDto(
                    (ulong)freshCombiId,
                    targetValue,
                    (ulong)who.Account.Id,
                    0, 0, 0,
                    0, 0, WireInbox,
                    combiTitle,
                    who.Account.Nickname ?? "",
                    stamp
                );

                await PushCombiPendingAsync(who, senderCombi, false);
                await SendCombiList(who);

                if (targetLive != null)
                {
                    await PushCombiPendingAsync(targetLive, targetCombi, true);
                    await SendCombiList(targetLive);
                }
            }
            catch (Exception error) when (IsCombiTableMissing(error))
            {
                WarnCombiTableMissing(error);

                var failDto = BuildCombiDto(
                    targetValue,
                    (ulong)who.Account.Id,
                    0,
                    0, 0, 0,
                    0, 0, 0,
                    combiTitle,
                    mateNick,
                    stamp
                );

                await who.SendAsync(new CombiActionAckMessage(1, 0, failDto));
            }
            catch (Exception error)
            {
                _log.Warning(error,
                    "[Combi] Action failed: player={PlayerId}, action={Action}, target={Target}",
                    who.Account.Id, verb, targetValue);

                var failDto = BuildCombiDto(
                    targetValue,
                    (ulong)who.Account.Id,
                    0,
                    0, 0, 0,
                    0, 0, 0,
                    combiTitle,
                    mateNick,
                    stamp
                );

                await who.SendAsync(new CombiActionAckMessage(1, 0, failDto));
            }
        }

        [MessageHandler(typeof(RoomInvitationPlayerReqMessage))]
        public void RoomInvitationPlayerReq(ChatSession session, RoomInvitationPlayerReqMessage message)
        {
            var who = session.Player;
            var invitee = ResolveInviteTarget(message.AccountId);

            _log.Information(
                "[ROOM INVITE] sender={Sender} raw={Raw} low32={Low32} high32={High32} target={Target}",
                who?.Account?.Nickname ?? "null",
                message.AccountId,
                (ulong)(uint)message.AccountId,
                message.AccountId >> 32,
                invitee?.Account?.Nickname ?? "null");

            if (who == null || invitee == null)
            {
                who?.SendAsync(new RoomInvitationPlayerAckMessage
                {
                    Unk1 = 3,
                    Unk2 = "",
                    Location = new PlayerLocationDto(),
                    Unk3 = 3
                });
                return;
            }

            switch (invitee.Settings.GetSetting("AllowRoomInvite"))
            {
                case CommunitySetting.Deny:
                    who.SendAsync(new RoomInvitationPlayerAckMessage
                    {
                        Unk1 = 2,
                        Unk2 = invitee.Account?.Nickname ?? "",
                        Location = invitee.Map<Player, PlayerLocationDto>(),
                        Unk3 = 2
                    });
                    return;

                case CommunitySetting.FriendOnly:
                    who.FriendManager.GetValue(invitee.Account.Id, out Friend link);
                    if (link == null)
                    {
                        who.SendAsync(new RoomInvitationPlayerAckMessage
                        {
                            Unk1 = 2,
                            Unk2 = invitee.Account?.Nickname ?? "",
                            Location = invitee.Map<Player, PlayerLocationDto>(),
                            Unk3 = 2
                        });
                        return;
                    }
                    break;
            }

            AllowRoomInviteEntry(invitee, who.Room);

            invitee.SendAsync(new RoomInvitationPlayerAckMessage
            {
                Location = who.Map<Player, PlayerLocationDto>(),
                Unk1 = message.AccountId,
                Unk2 = who.Account.Nickname,
                Unk3 = 1
            });
        }

        [MessageHandler(typeof(FriendActionReqMessage))]
        public void FriendActionRequest(ChatSession session, FriendActionReqMessage message)
        {
            var who = session.Player;
            if (who?.Account == null) return;
            if (message.AccountId == who.Account.Id)
                return;

            using (var authdb = AuthDatabase.Open())
            using (var db = GameDatabase.Open())
            {

                if (message.AccountId == 0 && !string.IsNullOrWhiteSpace(message.Nickname))
                {
                    var byName = DbUtil.Find<AccountDto>(authdb, statement => statement
                        .Where($"{nameof(AccountDto.Nickname):C} = @Name OR {nameof(AccountDto.Username):C} = @Name")
                        .WithParameters(new { Name = message.Nickname })).FirstOrDefault();
                    if (byName != null)
                        message.AccountId = (ulong)byName.Id;
                }
                if (message.AccountId == who.Account.Id)
                    return;

                var targetAccount = DbUtil.Find<AccountDto>(authdb, statement => statement
                    .Where($"{nameof(AccountDto.Id):C} = @Id")
                    .WithParameters(new { Id = message.AccountId })).FirstOrDefault();

                var targetPlayerRow = DbUtil.Find<PlayerDto>(db, statement => statement
                    .Where($"{nameof(PlayerDto.Id):C} = @Id")
                    .WithParameters(new { Id = message.AccountId })).FirstOrDefault();

                if (targetAccount == null || targetPlayerRow == null)
                {
                    who.SendAsync(new FriendActionAckMessage
                    {
                        Friend = new FriendDto(),
                        Result = FriendResult.UserNotExist,
                        Unk = 0
                    });
                    return;
                }

                var targetLive = GameServer.Instance.PlayerManager.Get(message.AccountId);
                who.FriendManager.GetValue(message.AccountId, out var link);

                switch (message.Action)
                {
                    case FriendAction.Add:
                        if (link != null)
                            return;

                        switch (targetLive.Settings.GetSetting("AllowFriendRequest"))
                        {
                            case CommunitySetting.Allow:
                                link = who.FriendManager.AddOrUpdate(message.AccountId, targetLive,
                                    FriendState.Requesting, FriendState.RequestDialog);

                                session.SendAsync(new FriendActionAckMessage
                                {
                                    Friend = link.GetFriend(),
                                    Result = FriendResult.Ok,
                                    Unk = 0
                                });

                                if (targetLive != null)
                                {
                                    targetLive.ChatSession?.SendAsync(new FriendActionAckMessage
                                    {
                                        Friend = link.GetPlayer(),
                                        Result = FriendResult.Ok,
                                        Unk = 0
                                    });
                                }

                                break;

                            case CommunitySetting.Deny:
                                session.SendAsync(new FriendActionAckMessage
                                {
                                    Friend = new FriendDto(),
                                    Result = FriendResult.UserNotExist,
                                    Unk = 0
                                });
                                break;
                        }

                        break;

                    case FriendAction.Decline:
                    case FriendAction.Remove:
                        if (link == null)
                        {
                            who?.Channel?.SendMessage(who, "System", "Cant find player!", NewChatType.Whisper);
                            return;
                        }

                        link.PlayerState = FriendState.NotInList;
                        link.FriendState = FriendState.NotInList;
                        who.FriendManager.Remove(message.AccountId, targetLive);

                        who.ChatSession?.SendAsync(new FriendActionAckMessage
                        {
                            Friend = link.GetFriend(),
                            Result = FriendResult.Ok,
                            Unk = 0
                        });
                        who.ChatSession?.SendAsync(
                            new FriendListAckMessage(who.FriendManager.Select(d => d.GetFriend())
                                .Where(x => x.State != 0).ToArray()));

                        if (targetLive != null)
                        {
                            targetLive.ChatSession?.SendAsync(new FriendActionAckMessage
                            {
                                Friend = link.GetPlayer(),
                                Result = FriendResult.Ok,
                                Unk = 0
                            });
                            targetLive.ChatSession?.SendAsync(
                                new FriendListAckMessage(targetLive.FriendManager.Select(d => d.GetFriend())
                                    .Where(x => x.State != 0).ToArray()));
                        }

                        break;

                    case FriendAction.Update:
                        if (link == null)
                            return;

                        link = who.FriendManager.AddOrUpdate(message.AccountId, targetLive,
                            FriendState.InList, FriendState.InList);

                        session.SendAsync(new FriendActionAckMessage
                        {
                            Friend = link.GetFriend(),
                            Result = FriendResult.Ok,
                            Unk = 0
                        });

                        if (targetLive != null)
                        {
                            targetLive.ChatSession?.SendAsync(new FriendActionAckMessage
                            {
                                Friend = link.GetPlayer(),
                                Result = FriendResult.Ok,
                                Unk = 0
                            });
                        }

                        break;

                    default:
                        Console.WriteLine("UNKNOWN FriendAction:" + message.Action);
                        break;
                }
            }
        }

        [MessageHandler(typeof(DenyActionReqMessage))]
        public void DenyHandler(ChatServer service, ChatSession session, DenyActionReqMessage message)
        {
            var who = session.Player;

            if (message.Deny.AccountId == who.Account.Id)
                return;

            Deny blocked;
            switch (message.Action)
            {
                case DenyAction.Add:
                    if (who.DenyManager.Contains(message.Deny.AccountId))
                        return;

                    var subject = GameServer.Instance.PlayerManager[message.Deny.AccountId];
                    if (subject == null)
                        return;

                    blocked = who.DenyManager.Add(subject);
                    session.SendAsync(new DenyActionAckMessage(0, DenyAction.Add, blocked.Map<Deny, DenyDto>()));
                    break;

                case DenyAction.Remove:
                    blocked = who.DenyManager[message.Deny.AccountId];
                    if (blocked == null)
                        return;

                    who.DenyManager.Remove(message.Deny.AccountId);
                    session.SendAsync(new DenyActionAckMessage(0, DenyAction.Remove, blocked.Map<Deny, DenyDto>()));
                    break;
            }
        }
    }
}

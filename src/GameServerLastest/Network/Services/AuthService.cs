namespace Santana.Network.Services
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using SantanaLib.Security.Cryptography;
    using Dapper;
    using Dapper.FastCrud;
    using Data.Chat;
    using Data.Club;
    using Data.Game;
    using Database.Auth;
    using Database.Game;
    using ExpressMapper.Extensions;
    using Message.Chat;
    using Message.Club;
    using Message.Game;
    using Message.Relay;
    using Newtonsoft.Json;
    using ProudNetSrc.Handlers;
    using Resource;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.GameRule;
    using Serilog;
    using Serilog.Core;
    using MySqlConnector;
    using System.Diagnostics.Metrics;
    using Santana.Network.Message.Event;
    internal class AuthService : ProudMessageHandler
    {
        private static readonly Version _clientVersion = new Version(0, 9, 45, 35312);
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(AuthService));
        private const int AttendanceSlotTotal = 28;
        private const int AttendanceBonusSlotThree = 22;
        private const int AttendanceBonusSlotSix = 23;
        private const int AttendanceBonusFlagThree = 1 << 7;
        private const int AttendanceBonusFlagSix = 1 << 8;
        private static uint[] LoadDailyAttendanceItems()
        {
            var board = new uint[AttendanceSlotTotal];
            using (var connection = GameDatabase.Open())
            {
                foreach (var row in DbUtil.Find<DailyAttendanceRewardDto>(connection))
                {
                    var value = IsDailyAttendanceRewardUsable(row.ItemKey) ? row.ItemKey : 0u;
                    var index = row.ItemIndex;
                    if (index >= 1 && index <= AttendanceSlotTotal)
                        board[index - 1] = value;
                    else if (index >= 0 && index < AttendanceSlotTotal)
                        board[index] = value;
                }
            }
            return board;
        }
        private static bool IsDailyAttendanceRewardUsable(uint itemKey)
        {
            if (itemKey == 0)
                return false;
            try
            {
                var shopItem = GameServer.Instance.ResourceCache.GetShop().GetItem(itemKey);
                if (shopItem?.ItemInfos == null)
                    return false;
                foreach (var info in shopItem.ItemInfos)
                {
                    if (info?.PriceGroup != null && info.PriceGroup.PriceType != ItemPriceType.None)
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
        private static int GetDailyAttendanceWeekKey()
        {
            var today = DateTime.Now;
            var weekNumber = CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                today,
                CalendarWeekRule.FirstFourDayWeek,
                DayOfWeek.Sunday);
            return today.Year * 100 + weekNumber;
        }
        private static PlayerDailyAttendanceStateDto LoadDailyAttendanceState(Player player)
        {
            using (var connection = GameDatabase.Open())
            {
                var week = GetDailyAttendanceWeekKey();
                return DbUtil.Find<PlayerDailyAttendanceStateDto>(connection, statement => statement
                    .Where($"{nameof(PlayerDailyAttendanceStateDto.PlayerId):C} = @PlayerId AND {nameof(PlayerDailyAttendanceStateDto.WeekKey):C} = @WeekKey")
                    .WithParameters(new
                    {
                        PlayerId = (int)player.Account.Id,
                        WeekKey = week
                    }))
                    .FirstOrDefault();
            }
        }
        private static IReadOnlyList<DailyAttendanceRewardDto> LoadDailyAttendanceRewardsForSlots(int firstSlot, int lastSlot)
        {
            using (var connection = GameDatabase.Open())
            {
                return DbUtil.Find<DailyAttendanceRewardDto>(connection, statement => statement
                    .Where($"{nameof(DailyAttendanceRewardDto.ItemIndex):C} >= @FirstSlot AND {nameof(DailyAttendanceRewardDto.ItemIndex):C} <= @LastSlot")
                    .WithParameters(new
                    {
                        FirstSlot = firstSlot,
                        LastSlot = lastSlot
                    }))
                    .Where(r => r.ItemKey != 0)
                    .OrderBy(r => r.ItemIndex)
                    .ToArray();
            }
        }
        private static IReadOnlyList<DailyAttendanceRewardDto> LoadDailyAttendanceRewardsForDay(DayOfWeek day)
        {
            var startSlot = (int)day * 3 + 1;
            return LoadDailyAttendanceRewardsForSlots(startSlot, startSlot + 2);
        }
        private static int GetDailyAttendanceDayBit(DayOfWeek day)
        {
            return 1 << (int)day;
        }
        private static int GiveDailyAttendanceRewards(Player player, IEnumerable<DailyAttendanceRewardDto> rewards)
        {
            var granted = 0;
            foreach (var reward in rewards)
            {
                try
                {
                    if (!IsDailyAttendanceRewardUsable(reward.ItemKey))
                    {
                        continue;
                    }
                    player.Inventory.Create(reward.ItemKey, (ushort)reward.Period, (byte)reward.Color, new EffectNumber[0], 1);
                    granted++;
                }
                catch (Exception ex)
                {
                }
            }
            return granted;
        }
        private static async Task ClaimDailyAttendanceAsync(Player player)
        {
            var weekday = DateTime.Now.DayOfWeek;
            var weekdayFlag = GetDailyAttendanceDayBit(weekday);
            var week = GetDailyAttendanceWeekKey();
            var existing = LoadDailyAttendanceState(player);
            var previousMask = existing?.ClaimedMask ?? 0;
            var mask = previousMask;
            var alreadyClaimedToday = (previousMask & weekdayFlag) != 0;
            if (!alreadyClaimedToday)
            {
                var granted = GiveDailyAttendanceRewards(player, LoadDailyAttendanceRewardsForDay(weekday));
                if (granted > 0)
                    mask |= weekdayFlag;
                else
                    { }
            }
            var claimedDays = 0;
            for (var bit = 0; bit < 7; bit++)
            {
                if ((mask & (1 << bit)) != 0)
                    claimedDays++;
            }
            var totalClaimed = (byte)claimedDays;
            if (totalClaimed >= 3 && (mask & AttendanceBonusFlagThree) == 0)
            {
                if (GiveDailyAttendanceRewards(player, LoadDailyAttendanceRewardsForSlots(AttendanceBonusSlotThree, AttendanceBonusSlotThree)) > 0)
                    mask |= AttendanceBonusFlagThree;
            }
            if (totalClaimed >= 6 && (mask & AttendanceBonusFlagSix) == 0)
            {
                if (GiveDailyAttendanceRewards(player, LoadDailyAttendanceRewardsForSlots(AttendanceBonusSlotSix, AttendanceBonusSlotSix)) > 0)
                    mask |= AttendanceBonusFlagSix;
            }
            using (var connection = GameDatabase.Open())
            {
                if (existing == null)
                {
                    var affected = await connection.ExecuteAsync(
                        @"INSERT INTO player_daily_attendance_state
                          (PlayerId, WeekKey, ClaimedMask, TotalClaimed, LastClaimDate)
                          VALUES (@PlayerId, @WeekKey, @ClaimedMask, @TotalClaimed, CURDATE())",
                        new
                        {
                            PlayerId = (int)player.Account.Id,
                            WeekKey = week,
                            ClaimedMask = mask,
                            TotalClaimed = totalClaimed
                        });
                }
                else
                {
                    var affected = await connection.ExecuteAsync(
                        @"UPDATE player_daily_attendance_state
                          SET ClaimedMask = @ClaimedMask,
                              TotalClaimed = @TotalClaimed,
                              LastClaimDate = CURDATE()
                          WHERE PlayerId = @PlayerId AND WeekKey = @WeekKey",
                        new
                        {
                            PlayerId = (int)player.Account.Id,
                            WeekKey = week,
                            ClaimedMask = mask,
                            TotalClaimed = totalClaimed
                        });
                }
            }
            await SendDailyAttendanceAsync(player);
        }
        private static Task SendDailyAttendanceAsync(Player player)
        {
            var board = LoadDailyAttendanceItems();
            var state = LoadDailyAttendanceState(player);
            var mask = state?.ClaimedMask ?? 0;
            return player.SendAsync(new NewDailyAttendanceAckMessage(
                resultado: 1,
                items: board,
                domingo: (byte)((mask & 1) != 0 ? 1 : 0),
                lunes: (byte)((mask & 2) != 0 ? 1 : 0),
                martes: (byte)((mask & 4) != 0 ? 1 : 0),
                miercoles: (byte)((mask & 8) != 0 ? 1 : 0),
                jueves: (byte)((mask & 16) != 0 ? 1 : 0),
                viernes: (byte)((mask & 32) != 0 ? 1 : 0),
                sabado: (byte)((mask & 64) != 0 ? 1 : 0),
                diaactual: (uint)DateTime.Now.DayOfWeek,
                reclamados: state?.TotalClaimed ?? 0));
        }
        [MessageHandler(typeof(NewCheckDailyAttendanceReqMessage))]
        public async Task NewCheckDailyAttendanceReq(GameSession session, NewCheckDailyAttendanceReqMessage message)
        {
            if (session.Player == null)
                return;
            await ClaimDailyAttendanceAsync(session.Player);
        }
        [MessageHandler(typeof(PromotionAttendanceGiftItemReqMessage))]
        public async Task PromotionAttendanceGiftItemReq(GameSession session, PromotionAttendanceGiftItemReqMessage message)
        {
            if (session.Player == null)
                return;
            await ClaimDailyAttendanceAsync(session.Player);
        }
        [MessageHandler(typeof(LoginRequestReqMessage))]
        public async Task LoginHandler(GameSession session, LoginRequestReqMessage message)
        {
            if (session == null || message == null)
            {
                return;
            }
            if (message.AccountId == 0 || string.IsNullOrWhiteSpace(message.AuthToken) ||
                string.IsNullOrWhiteSpace(message.newToken) || string.IsNullOrWhiteSpace(message.Datetime))
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            AccountDto accountRow;
            using (var authDb = AuthDatabase.Open())
            {
                accountRow = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Id):C} = @Id")
                        .WithParameters(new { Id = message.AccountId })))
                    .FirstOrDefault();
            }
            if (accountRow == null)
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                await session.CloseAsync();
                return;
            }
            if (!accountRow.IsConnected)
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                await session.CloseAsync();
                return;
            }
            message.AccountId = (ulong)accountRow.Id;
            message.Username = accountRow.Username;
            if (GameServer.Instance.PlayerManager.Count >= Config.Instance.PlayerLimit)
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.ServerFull));
                return;
            }
            var hwidLetters = System.Text.RegularExpressions.Regex.Replace(accountRow.Hwid, "[^a-zA-Z]", "");
            var derivedSessionId = Hash.GetUInt32<CRC32>($"<{accountRow.Username}+{accountRow.Password}+{hwidLetters}>");
            var expectedAuthToken = Hash.GetString<CRC32>($"<{accountRow.Username}+{derivedSessionId}+{message.Datetime}>");
            if (expectedAuthToken != message.AuthToken)
            {
                _log.ForAccount(message.AccountId, message.Username).Error("Recomputed auth digest disagrees with the value presented by the client; ticket refused");
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            var expectedNewToken = Hash.GetString<CRC32>($"<{expectedAuthToken}+{derivedSessionId}>");
            if (expectedNewToken != message.newToken)
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            var nowUnix = DateTimeOffset.Now.ToUnixTimeSeconds();
            var activeBan = accountRow.Bans.FirstOrDefault(b => b.Date + (b.Duration ?? 0) > nowUnix);
            if (activeBan != null)
            {
                var banExpiry = DateTimeOffset.FromUnixTimeSeconds(activeBan.Date + (activeBan.Duration ?? 0));
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                return;
            }
            var accountModel = new Account(accountRow);
            if (accountModel.SecurityLevel < Config.Instance.SecurityLevel)
            {
                await session.SendAsync(new LoginReguestAckMessage(GameLoginResult.SessionTimeout));
                await session.CloseAsync();
                return;
            }
            if (GameServer.Instance.PlayerManager.Contains(accountModel.Id))
            {
                _log.ForAccount(accountModel).Information("A prior live session holds this identity; tearing it down to make room");
                var previousPlayer = GameServer.Instance.PlayerManager.Get(accountModel.Id);
                GameServer.Instance.PlayerManager.Remove(previousPlayer);
                previousPlayer?.Session.CloseAsync();
            }
            _log.ForAccount(accountModel).Information("Credentials cleared upstream; proceeding to hydrate the profile");
            await Task.Run(async () =>
            {
                using (var gameDb = GameDatabase.Open())
                {
                    var playerRow = (await DbUtil.FindAsync<PlayerDto>(gameDb, statement => statement
                            .Include<PlayerCharacterDto>(join => join.LeftOuterJoin())
                            .Include<PlayerDenyDto>(join => join.LeftOuterJoin())
                            .Include<PlayerFriendDto>(join => join.LeftOuterJoin())
                            .Include<PlayerSettingDto>(join => join.LeftOuterJoin())
                            .Include<PlayerItemDto>(join => join.LeftOuterJoin())
                            .Include<PlayerMailDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                            .WithParameters(new { Id = message.AccountId })))
                        .FirstOrDefault();
                    var statsRow = (await DbUtil.FindAsync<PlayerDto>(gameDb, statement => statement
                            .Include<PlayerDeathMatchDto>(join => join.LeftOuterJoin())
                            .Include<PlayerTouchDownDto>(join => join.LeftOuterJoin())
                            .Include<PlayerChaserDto>(join => join.LeftOuterJoin())
                            .Include<PlayerBattleRoyalDto>(join => join.LeftOuterJoin())
                            .Include<PlayerCaptainDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                            .WithParameters(new { Id = message.AccountId })))
                        .FirstOrDefault();
                    var extraStatsRow = (await DbUtil.FindAsync<PlayerDto>(gameDb, statement => statement
                            .Include<PlayerSiegeDto>(join => join.LeftOuterJoin())
                            .Include<PlayerArenaDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                            .WithParameters(new { Id = message.AccountId })))
                        .FirstOrDefault();
                    var experienceTable = GameServer.Instance.ResourceCache.GetExperience();
                    Experience levelExperience;
                    if (playerRow == null)
                    {
                        if (!experienceTable.TryGetValue(Config.Instance.Game.StartLevel, out levelExperience))
                        {
                            levelExperience = new Experience { TotalExperience = 0 };
                        }
                        playerRow = new PlayerDto
                        {
                            Id = (int)accountModel.Id,
                            PlayTime = TimeSpan.FromSeconds(0).ToString(),
                            Level = Config.Instance.Game.StartLevel,
                            PEN = Config.Instance.Game.StartPEN,
                            AP = Config.Instance.Game.StartAP,
                            Coins1 = Config.Instance.Game.StartCoins1,
                            Coins2 = Config.Instance.Game.StartCoins2,
                            TotalExperience = levelExperience.TotalExperience,
                        };
                        try
                        {
                            await DbUtil.InsertAsync(gameDb, playerRow);
                        }
                        catch (Exception insertError)
                        {
                            session.Channel.Pipeline.FireExceptionCaught(insertError);
                            return;
                        }
                    }
                    else
                    {
                        if (!TimeSpan.TryParse(playerRow.PlayTime, out _))
                            playerRow.PlayTime = TimeSpan.FromSeconds(0).ToString();
                        if (!experienceTable.TryGetValue(playerRow.Level, out levelExperience))
                        {
                            levelExperience = new Experience { TotalExperience = 0 };
                        }
                        if (playerRow.TotalExperience < levelExperience.TotalExperience)
                        {
                            playerRow.TotalExperience = levelExperience.TotalExperience;
                            await DbUtil.UpdateAsync(gameDb, playerRow);
                        }
                    }
                    if (statsRow != null)
                    {
                        playerRow.DeathMatchInfo = statsRow.DeathMatchInfo;
                        playerRow.TouchDownInfo = statsRow.TouchDownInfo;
                        playerRow.ChaserInfo = statsRow.ChaserInfo;
                        playerRow.BattleRoyalInfo = statsRow.BattleRoyalInfo;
                        playerRow.CaptainInfo = statsRow.CaptainInfo;
                    }
                    if (extraStatsRow != null)
                    {
                        playerRow.SiegeInfo = extraStatsRow.SiegeInfo;
                        playerRow.ArenaInfo = extraStatsRow.ArenaInfo;
                    }
                    session.Player = new Player(session, accountModel, playerRow);
                }
                if (session.Player == null)
                {
                    _log.ForAccount(accountModel).Error("Profile hydration left the session without a player object; aborting the bring-up");
                    return;
                }
                GameServer.Instance.PlayerManager.Add(session.Player);
                var loginResult = string.IsNullOrWhiteSpace(accountModel.Nickname)
                    ? GameLoginResult.ChooseNickname
                    : GameLoginResult.OK;
                loginResult = session.Player.CharacterManager.Any() ? loginResult : GameLoginResult.ChooseNickname;
                if (session.UpdateShop)
                {
                    await ShopService.ShopUpdateMsg(session, false);
                    session.UpdateShop = false;
                }
                if (session.UpdateRandomShop)
                {
                }
                await session.SendAsync(new LoginReguestAckMessage(loginResult, session.Player.Account.Id));
                if (loginResult == GameLoginResult.OK)
                {
                    await LoginAsync(session);
                }
            });
            using (var authDb = AuthDatabase.Open())
            {
                var connectedAccount = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                         .Include<BanDto>(join => join.LeftOuterJoin())
                         .Where($"{nameof(AccountDto.Id):C} = @Id")
                         .WithParameters(new { Id = message.AccountId }))).FirstOrDefault();
                connectedAccount.Status = 1;
                await DbUtil.UpdateAsync(authDb, connectedAccount);
            }
        }
        public static async Task<bool> IsNickAvailableAsync(string nickname)
        {
            var restrictions = Config.Instance.Game.NickRestrictions;
            var asciiOnly = restrictions.AsciiOnly;
            if (asciiOnly)
            {
                if (nickname.Any(c => c > 127))
                    return false;
            }
            else
            {
                if (nickname.Any(c => c > 255))
                    return false;
            }
            if (!Namecheck.IsNameValid(nickname))
                return false;
            if (nickname.Length < restrictions.MinLength || nickname.Length > restrictions.MaxLength ||
                asciiOnly && Encoding.UTF8.GetByteCount(nickname) != nickname.Length)
                return false;
            var maxRepeat = restrictions.MaxRepeat;
            if (maxRepeat > 0)
            {
                var runLength = 1;
                var runChar = nickname[0];
                for (var i = 1; i < nickname.Length; i++)
                {
                    if (runChar != nickname[i])
                    {
                        if (runLength > maxRepeat)
                            return false;
                        runLength = 0;
                        runChar = nickname[i];
                    }
                    runLength++;
                }
            }
            var now = DateTimeOffset.Now.ToUnixTimeSeconds();
            using (var authDb = AuthDatabase.Open())
            {
                var nameTaken = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                        .Where($"{nameof(AccountDto.Nickname):C} = @{nameof(nickname)}")
                        .WithParameters(new { nickname })))
                    .Any();
                var nameReserved = (await DbUtil.FindAsync<NicknameHistoryDto>(authDb, statement => statement
                        .Where(
                            $"{nameof(NicknameHistoryDto.OldName):C} = @{nameof(nickname)} AND ({nameof(NicknameHistoryDto.ExpireDate):C} = -1 OR {nameof(NicknameHistoryDto.ExpireDate):C} > @{nameof(now)})")
                        .WithParameters(new { nickname, now })))
                    .Any();
                return !nameTaken && !nameReserved;
            }
        }
        public static void MissionAsync(GameSession session)
        {
            var date = DateTime.Now.ToString("dddd, dd MMMM yyyy");
            var player = session.Player;
            using (var gameDb = GameDatabase.Open())
            {
                var savedMission = DbUtil.Find<Daily_MissionDto>(gameDb, statement => statement
                    .Where($"{nameof(Daily_MissionDto.PlayerId):C} = @{nameof(session.Player.Account.Id)} AND ({nameof(Daily_MissionDto.Date):C} = @{nameof(date)})")
                    .WithParameters(new { session.Player.Account.Id, date })).FirstOrDefault();
                if (savedMission == null)
                {
                    var random = new SecureRandom();
                    var firstReward = random.Next(1, 10);
                    var secondReward = random.Next(1, 10);
                    var thirdReward = random.Next(1, 10);
                    var mapChoice = random.Next(0, 12);
                    DbUtil.Insert(gameDb, new Daily_MissionDto
                    {
                        PlayerId = session.Player.Account.Id,
                        Map = mapChoice,
                        MaxProgress = 1,
                        Progress = 1,
                        Reward = firstReward,
                        Reward2 = secondReward,
                        Reward3 = thirdReward,
                        IsRewarded = false,
                        Date = date
                    });
                    player?.SendAsync(new DailyMission_NoticeMessage { Unk = 1, GameMode = 0, Map = mapChoice, MaxProgress = 1, Progress = 0, Unk5 = 5, Unk6 = new int[] { firstReward, secondReward, thirdReward } });
                }
                else
                {
                    var step = Math.Clamp(savedMission.Progress <= 0 ? 1 : savedMission.Progress, 1, 3);
                    player?.SendAsync(new DailyMission_NoticeMessage { Unk = 1, GameMode = 0, Map = savedMission.Map, MaxProgress = step, Progress = 0, Unk5 = 5, Unk6 = new int[] { savedMission.Reward, savedMission.Reward2, savedMission.Reward3 } });
                }
            }
        }
        public static void LoadPlayerNameTag(Player plr, bool IsSwitch, bool RankChannel)
        {
            if (plr == null)
                return;
            ShopService.RefreshCollectBookRuntimeState(plr);
            var equippedNameTag = plr.CharacterManager.Boosts.GetItem(BoostSlot.NameTag);
            plr.NameTag = BoostManager.ResolveVisibleNameTagId(equippedNameTag);
            if (plr.CollectBookNameTag > 0)
                plr.NameTag = plr.CollectBookNameTag;
            plr.SendAsync(new CollectBookInvenEffectInfoAckMessage
            {
                Unk = 1,
                active = (byte)(plr.NameTag > 0 ? 1 : 0),
                Unk3 = 0,
                Unk4 = 0,
                nametagid = plr.NameTag > 0 ? plr.NameTag : 0,
                Unk5 = 0,
                Unk6 = 0,
                days = "DAYS",
                nametag = "NAMETAGS",
                zero = "00000000000000",
                zero2 = "00000000000000",
                zero3 = "00000000000000"
            });
            if (IsSwitch)
            {
                if (plr.Room != null)
                    plr.Room.Broadcast(new RoomEnterPlayerInfoListForNameTagAckMessage(plr.Room.Players.Values
                        .Select(player => new NameTagDto(player.Account.Id, player.NameTag)).ToArray()));
                if (plr.Channel != null)
                    plr.Channel.Broadcast(new Chennel_PlayerNameTagList_AckMessage(plr.Channel.Players.Values
                        .Select(p => p.Map<Player, PlayerNameTagInfoDto>()).ToArray()));
            }
        }
        public static async Task LoginAsync(GameSession session)
        {
            var plr = session.Player;
            plr.HeartBeat = DateTime.Now.AddMinutes(2);
            plr.LoggedIn = true;
            try
            {
                plr.IP = session.RemoteEndPoint.Address.ToString();
                plr?.SendAsync(new MoneyRefreshCashInfoAckMessage(plr.PEN, plr.AP));
                plr?.SendAsync(new CharacterCurrentSlotInfoAckMessage
                {
                    ActiveCharacter = plr.CharacterManager.CurrentSlot,
                    CharacterCount = (byte)plr.CharacterManager.Count,
                    MaxSlots = 3
                });
                plr?.SendAsync(new ShoppingBasketListInfoAckMessage(plr.ShoppingBasketManager.ToArray()));
                MissionAsync(session);
                plr?.SendAsync(new MoenyRefreshCoinInfoAckMessage(plr.Coins1, plr.Coins2));
                plr?.SendAsync(new Btc_Sync_NoticeMessage { Unk = 1, Unk2 = 9, Unk3 = 0, Unk4 = 10 });
                try
                {
                    var collectBookVersion = ShopService.GetCollectBookVersion();
                    await session.SendAsync(new ItemInventoryInfoAckMessage
                    {
                        Items = plr.Inventory.Select(i => i.Map<PlayerItem, ItemDto>()).ToArray()
                    });
                    await session.SendAsync(ShopService.CreateCollectBookInventoryInfoAck(plr, true));
                    await session.SendAsync(new CollectBook_UpdateRequest_Ack());
                    await session.SendAsync(new CollectBook_UpdateCheck_Ack
                    {
                        Data = collectBookVersion
                    });
                    var shopVersion = ShopService.GetShopVersion();
                    await session.SendAsync(new NewShopUpdateCheckAckMessage
                    {
                        Date01 = shopVersion,
                        Date02 = shopVersion,
                        Date03 = shopVersion,
                        Date04 = shopVersion,
                        Unk = 0
                    });
                }
                catch
                {
                }
                plr?.SendAsync(new PlayeArcadeMapInfoAckMessage());
                plr?.SendAsync(new PlayerArcadeStageInfoAckMessage());
                ShopService.RefreshCollectBookRuntimeState(plr);
                plr.CharacterManager.Boosts.PlayerNameTag();
                plr?.SendAsync(new ClubMyInfoAckMessage(plr.Map<Player, ClubMyInfoDto>()));
                foreach (var character in plr.CharacterManager)
                {
                    plr?.SendAsync(new CharacterCurrentInfoAckMessage
                    {
                        Slot = character.Slot,
                        Style = new CharacterStyle(character.Gender, character.Slot)
                    });
                    plr?.SendAsync(new CharacterCurrentItemInfoAckMessage
                    {
                        Slot = character.Slot,
                        Weapons = character.Weapons.GetItems().Select(i => i?.Id ?? 0).ToArray(),
                        Skills = new[] { character.Skills.GetItem(SkillSlot.Skill)?.Id ?? 0 },
                        Clothes = character.Costumes.GetItems().Select(i => i?.Id ?? 0).ToArray()
                    });
                }
            }
            finally
            {
                await plr?.SendAsync(new ItemEquipBoostItemInfoAckMessage { Items = plr.CharacterManager.Boosts.GetItems().Select(i => i?.Id ?? 0).ToArray() });
                using (var gameDb = GameDatabase.Open())
                {
                    var esperChip = DbUtil.Find<EsperSkillDto>(gameDb, statement => statement
                       .Where($"{nameof(EsperSkillDto.PlayerId):C} = @{nameof(plr.Account.Id)} AND {nameof(EsperSkillDto.CharId):C} = @{nameof(plr.CharacterManager.CurrentSlot)}")
                       .WithParameters(new { plr.Account.Id, plr.CharacterManager.CurrentSlot })).FirstOrDefault();
                    if (esperChip == null)
                        await plr?.SendAsync(new EspherChipLv5Message());
                    else
                        await plr?.SendAsync(new EspherChipLv5Message(esperChip.Id));
                }
                plr.EDHwid = System.Text.RegularExpressions.Regex.Replace(plr.Account.Hwid, "[^a-zA-Z]", "");
                plr?.SendAsync(new ItemClearInvalidEquipItemAckMessage());
                plr?.SendAsync(new ItemClearEsperChipAckMessage());
                plr?.SendAsync(new MapOpenInfosMessage());
                await plr.SendAsync(new PlayerAccountInfoAckMessage(plr.Map<Player, PlayerAccountInfoDto>()));
                await plr.SendAsync(new ServerResultAckMessage(ServerResult.WelcomeToS4World));

                try
                {
                    await ClaimDailyAttendanceAsync(plr);
                }
                catch (Exception e)
                {
                    _log.Error(e, "Daily sign-in reward could not be granted at bring-up for {PlayerId}", plr.Account.Id);
                }
            }
        }
        [MessageHandler(typeof(LoginReqMessage))]
        public async Task ChatLoginHandler(ChatSession session, LoginReqMessage message)
        {
            var player = GameServer.Instance.PlayerManager[message.AccountId];
            if (player == null)
            {
                await session.SendAsync(new LoginAckMessage(3));
                await session.CloseAsync();
                return;
            }
            _log.ForAccount(player).Information("Messaging socket opened by {remoteEndPoint}; validating it against the live game link", session.RemoteEndPoint);
            var chatEndPoint = session.RemoteEndPoint;
            var gameEndPoint = player.Session.RemoteEndPoint;
            if (!gameEndPoint.Address.Equals(chatEndPoint.Address))
            {
                player.Disconnect();
                await session.CloseAsync();
                return;
            }
            if (player.ChatSession != null)
            {
                player.Disconnect();
                await session.CloseAsync();
                return;
            }
            session.GameSession = player.Session;
            player.ChatSession = session;
            _log.ForAccount(player).Information("Messaging socket paired with its game link; the account is fully online");
            try
            {
                await session.SendAsync(new LoginAckMessage(0));
                await ClubService.SendOwnClubOverviewOnLoginAsync(player.Session);
                var nameColorTag = "";
                if (!string.IsNullOrWhiteSpace(player.Account.AccountDto.Color))
                    nameColorTag = "{" + player.Account.AccountDto.Color + "}";
                var clanBadge = "";
                if (player.Club?.Id > 0)
                {
                    if (Convert.ToInt32(player.Club.ClanIcon.Split(new string[] { "-" }, StringSplitOptions.None)[1]) == 0)
                    {
                        clanBadge = "logo_" + (Convert.ToInt32(player.Club.ClanIcon.Split(new string[] { "-" }, StringSplitOptions.None)[2]) - 1).ToString() ?? "0";
                    }
                    else
                    {
                        clanBadge = "basic_" + player.Club.ClanIcon.Split(new string[] { "-" }, StringSplitOptions.None)[1] ?? "0";
                    }
                }
                await session.SendAsync(new DenyListAckMessage(player.DenyManager.Select(d => d.Map<Deny, DenyDto>()).ToArray()));
                await session.SendAsync(
                    new FriendListAckMessage(player.FriendManager.Select(d => d.GetFriend())
                        .Where(x => x.State != 0).ToArray()));
                await CommunityService.SendCombiList(player);
                await CommunityService.SendPendingCombiRequests(player);
                var contactList = player.FriendManager.Select(d => d.Map<Friend, PlayerInfoDto>()).ToList();
                foreach (var combiInfo in await CommunityService.GetCombiPlayerInfos(player))
                {
                    if (contactList.All(x => x.Info.AccountId != combiInfo.Info.AccountId))
                        contactList.Add(combiInfo);
                }
                if (player.Club?.Id > 0)
                {
                    foreach (var clubMate in player.Club.Players.Select(d => d.Value.Map<ClubPlayerInfo, PlayerInfoDto>()))
                    {
                        if (contactList.All(x => x.Info.AccountId != clubMate.Info.AccountId))
                            contactList.Add(clubMate);
                    }
                    Club.LogOn(player);
                }
                player.LoggedFinish = true;
                await session.SendAsync(new ChatPlayerInfoListAckMessage(contactList.ToArray()));
                Club.SendAllLivePresenceTo(player, "AUTH.CHATLOGIN.SNAPSHOT");
                Club.BroadcastLivePresence(player, "AUTH.CHATLOGIN.BROADCAST");
            }
            catch (Exception ex)
            {
                session.Channel.Pipeline.FireExceptionCaught(ex);
            }
        }
    }
}

namespace Santana
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SantanaLib.IO;
    using Dapper;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Club;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using Santana.Network.Message.Relay;
    using Santana.Network.Services;
    using ProudNetSrc;
    using Serilog;
    using Serilog.Core;
    internal class Player : IDisposable
    {
        private static readonly ILogger PlayerLogger = Log.ForContext(Constants.SourceContextPropertyName, "GamePlayerMgr");
        private uint apPoints;
        private uint coinBalanceA;
        private uint coinBalanceB;
        private byte currentLevel;
        private uint penBalance;
        private string accumulatedPlayTime;
        private uint experienceTotal;
        private uint defeatCount;
        private uint victoryCount;
        private byte tutorialProgress;
        private uint nameTagId;
        private bool joinPermitted;
        public List<EffectNumber> effects;
        public List<EffectNumber> CollectBookEffects;
        public float CollectBookPenRate;
        public float CollectBookExpRate;
        public uint CollectBookNameTag;
        public bool LoggedIn = false;
        public bool LoggedFinish = false;
        public bool IsChallengeReg = false;
        public bool IsRankReg = false;
        public string GameRItem = "";
        public string GameRCard = "";
        public string GameRSupplies = "";
        public string GameREvent = "";
        public uint ViewingOtherClubId;
        public bool PreferOtherClubNotice;
        public DateTime LastOtherClubInfoUtc;
        public float PlayerHP = 0;
        public DateTime HeartBeat;
        public string EDHwid = "";
        public string Hwid = "";
        public string IP = "";
        public StatsManager stats;
        public ItemStatsManager ItemStats;
        public Player(GameSession session, Account account, PlayerDto dto)
        {
            Session = session;
            Account = account;
            accumulatedPlayTime = dto.PlayTime;
            tutorialProgress = dto.TutorialState;
            currentLevel = dto.Level;
            experienceTotal = dto.TotalExperience;
            penBalance = (uint)dto.PEN;
            apPoints = (uint)dto.AP;
            coinBalanceA = (uint)dto.Coins1;
            coinBalanceB = (uint)dto.Coins2;
            TotalMatches = (uint)dto.TotalMatches;
            defeatCount = (uint)dto.TotalLosses;
            victoryCount = (uint)dto.TotalWins;
            nameTagId = dto.TagId;
            stats = new StatsManager(this, dto);
            ItemStats = new ItemStatsManager(this);
            Settings = new PlayerSettingManager(this, dto);
            DenyManager = new DenyManager(this, dto);
            FriendManager = new FriendManager(this, dto);
            Mailbox = new Mailbox(this, dto);
            ShoppingBasketManager = new ShoppingBasketManager(this, dto);
            Inventory = new Inventory(this, dto);
            CharacterManager = new CharacterManager(this, dto);
            Club = GameServer.Instance.ClubManager.GetClubByAccount(account.Id);
            effects = new EffectNumber[0].ToList();
            CollectBookEffects = new EffectNumber[0].ToList();
            CollectBookPenRate = 0f;
            CollectBookExpRate = 0f;
            CollectBookNameTag = 0;
            Room = null;
            RoomInfo = new PlayerRoomInfo();
            PlayerCoinBuff = new PlayerCoinBuff(this);
            LuckyShot = new PlayerLuckyShot(this);
            DailyMission = new DailyMissionManager(this);
            AchieveMission = new AchieveMissionManager(this);
        }
        public string PlayerLocation()
        {
            var whereText = "";
            if (Channel == null)
                whereText = "Channels";
            if (Channel.Id < 2)
                return "Channels";
            if (Channel.Id == 2)
                whereText = "Free";
            else if (Channel.Id == 3)
                whereText = "Competitive";
            else if (Channel.Id == 4)
                whereText = "Clan War";
            if (Room != null)
                whereText += "Room " + Room.Id.ToString();
            return whereText;
        }
        public bool GainExp(int amount)
        {
            if (Disposed)
                return false;
            if (amount <= 0)
                return false;
            if (Room.Options.IsFriendly)
            {
                PlayerLogger.ForAccount(this)
                    .Information(Account.Nickname + "(ID: " + Account.Id + ")" + " Gained no EXP for Playing Friendly Mode");
                return false;
            }
            PlayerLogger.ForAccount(this)
                .Debug(Account.Nickname + "(ID: " + Account.Id + ")" + " Gained {amount} exp", amount);
            var expChart = GameServer.Instance.ResourceCache.GetExperience();
            var levelEntry = expChart.GetValueOrDefault(Level);
            if (levelEntry == null)
            {
                return false;
            }
            if (levelEntry.ExperienceToNextLevel == 0 || Level >= Config.Instance.Game.MaxLevel)
                return false;
            var didLevelUp = false;
            var levelsClimbed = 0;
            TotalExperience += (uint)amount;
            while (levelEntry.ExperienceToNextLevel != 0 &&
                   levelEntry.ExperienceToNextLevel <= (int)(TotalExperience - levelEntry.TotalExperience))
            {
                var nextLevel = Level + 1;
                levelEntry = expChart.GetValueOrDefault(nextLevel);
                if (levelEntry == null)
                {
                    break;
                }
                PlayerLogger.ForAccount(this)
                    .Information("Enough exp for the next rank, now at {level}", nextLevel);
                Level++;
                levelsClimbed++;
                using (var conn = GameDatabase.Open())
                {
                    var rewardRow = DbUtil.Find<LevelRewardDto>(conn, statement => statement
                            .Where($"{nameof(LevelRewardDto.Level):C} = @{nameof(Level)}")
                            .WithParameters(new { Level })).FirstOrDefault();
                    if (rewardRow.Level == Level)
                    {
                        if (rewardRow.Reward != 0)
                            Inventory.CreateUnits(rewardRow.Reward, rewardRow.Units);
                        if (rewardRow.AP != 0 && rewardRow.PEN != 0)
                        {
                            AP += rewardRow.AP;
                            PEN += rewardRow.PEN;
                            Session.Player.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel,
                                Session.Player.Account.Id, "Server", $"You reached level {Level}! You got {rewardRow.AP} AP & You got {rewardRow.PEN} PEN."));
                        }
                    }
                }
                didLevelUp = true;
            }
            if (levelsClimbed > 0)
                AddCombiLevelExp(levelsClimbed, levelsClimbed * 20);
            Session.Player.Session.SendAsync(new MoneyRefreshCashInfoAckMessage(Session.Player.PEN, Session.Player.AP));
            Session.SendAsync(new ExpRefreshInfoAckMessage(TotalExperience));
            Session.SendAsync(new PlayerAccountInfoAckMessage(this.Map<Player, PlayerAccountInfoDto>()));
            return didLevelUp;
        }
        public void AddCombiMatchStats(bool won)
        {
            if (Disposed || Account == null || Room?.Options?.IsFriendly == true)
                return;
            try
            {
                using (var conn = GameDatabase.Open())
                {
                    var affected = conn.Execute(
                        $@"UPDATE combi
                           SET Battle = Battle + 1,
                               `Match` = `Match` + 1,
                               Win = Win + @Win,
                               Defeat = Defeat + @Defeat
                           WHERE (State = 0 OR State = 1)
                             AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                        new
                        {
                            PlayerId = Account.Id,
                            Win = won ? 1 : 0,
                            Defeat = won ? 0 : 1
                        });
                    if (affected > 0)
                    {
                        PlayerLogger.ForAccount(this)
                            .Information("[Combi] Wrote the match result for player={PlayerId}, won={Won}, rows touched={Rows}",
                                Account.Id, won, affected);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        private void AddCombiLevelExp(int levelsGained, int combiExp)
        {
            if (levelsGained <= 0 || combiExp <= 0 || Account == null || Room?.GameState != GameState.Result)
                return;
            try
            {
                using (var conn = GameDatabase.Open())
                {
                    var affected = conn.Execute(
                        @"UPDATE combi
                          SET Exp = Exp + @Exp
                          WHERE (State = 0 OR State = 1)
                            AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                        new { PlayerId = Account.Id, Exp = combiExp });
                    if (affected > 0)
                    {
                        PlayerLogger.ForAccount(this)
                            .Information("[Combi] Credited pair exp for player={PlayerId}, levels={Levels}, combiExp={CombiExp}, rows touched={Rows}",
                                Account.Id, levelsGained, combiExp, affected);
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }
        public uint GetCombiMasterExp()
        {
            if (Account == null)
                return 0;
            try
            {
                using (var conn = GameDatabase.Open())
                {
                    var summed = conn.ExecuteScalar<long>(
                        @"SELECT COALESCE(SUM(Exp), 0)
                          FROM combi
                          WHERE (State = 0 OR State = 1)
                            AND (PlayerId = @PlayerId OR CombiPlayerId = @PlayerId)",
                        new { PlayerId = Account.Id });
                    return summed <= 0 ? 0 : (uint)Math.Min(summed, uint.MaxValue);
                }
            }
            catch (Exception ex)
            {
                return 0;
            }
        }
        public void SendConsoleMessage(string message)
        {
            SendAsync(new AdminActionAckMessage { Result = 0, Message = message });
        }
        public void SendNotice(string message)
        {
            SendAsync(new NoticeAdminMessageAckMessage(message));
        }
        public void Save()
        {
            if (Disposed)
                return;
            using (var conn = GameDatabase.Open())
            {
                if (NeedsToSave)
                {
                    var snapshot = new PlayerDto
                    {
                        Id = (int)Account.Id,
                        PlayTime = PlayTime,
                        TutorialState = TutorialState,
                        Level = Level,
                        TotalExperience = TotalExperience,
                        PEN = (int)PEN,
                        AP = (int)AP,
                        Coins1 = (int)Coins1,
                        Coins2 = (int)Coins2,
                        CurrentCharacterSlot = CharacterManager.CurrentSlot,
                        TotalMatches = (int)(TotalWins + TotalLosses),
                        TotalLosses = (int)TotalLosses,
                        TotalWins = (int)TotalWins,
                        TagId = (uint)NameTag
                    };
                    DbUtil.Update(conn, snapshot);
                    NeedsToSave = false;
                }
                Settings.Save(conn);
                Inventory.Save(conn);
                CharacterManager.Save(conn);
                DenyManager.Save(conn);
                Mailbox.Save(conn);
                stats.Save(conn);
                CharacterManager.Boosts.Save(conn);
            }
        }
        public Task SendAsync(object message)
        {
            if (Disposed || message == null)
                return Task.CompletedTask;
            return ProudNetSrc.Serialization.Packet.ChannelOf(message) switch
            {
                ProudNetSrc.Serialization.PacketType.Chat => ChatSession?.SendAsync(message) ?? Task.CompletedTask,
                _ => Session?.SendAsync(message) ?? Task.CompletedTask,
            };
        }
        public void KickToServerSelectionForNameTagChange(uint oldNameTag, uint newNameTag)
        {
        }
        public void KickToServerSelectionAfterCollectBookChange()
        {
        }
        public void Disconnect()
        {
            Session?.Dispose();
        }
        #region Properties
        internal bool NeedsToSave { get; set; }
        public GameSession Session { get; set; }
        public ChatSession ChatSession { get; set; }
        public PlayerSettingManager Settings { get; private set; }
        public DenyManager DenyManager { get; private set; }
        public FriendManager FriendManager { get; private set; }
        public Mailbox Mailbox { get; private set; }
        public PlayerCoinBuff PlayerCoinBuff { get; set; }
        public PlayerLuckyShot LuckyShot { get; set; }
        public DailyMissionManager DailyMission { get; set; }
        public AchieveMissionManager AchieveMission { get; set; }
        public ShoppingBasketManager ShoppingBasketManager { get; private set; }
        public Account Account { get; set; }
        public Club Club { get; set; }
        public CharacterManager CharacterManager { get; private set; }
        public Inventory Inventory { get; private set; }
        public Channel Channel { get; internal set; }
        public Room Room { get; internal set; }
        public PlayerRoomInfo RoomInfo { get; private set; }
        private DateTimeOffset lastPresenceStamp = DateTimeOffset.Now;
        public bool Disposed { get; private set; }
        public TimeSpan OnTimeSpan
        {
            get
            {
                var elapsed = DateTimeOffset.Now - lastPresenceStamp;
                lastPresenceStamp = DateTimeOffset.Now;
                return elapsed;
            }
        }
        public string PlayTime
        {
            get
            {
                if (accumulatedPlayTime == "")
                    accumulatedPlayTime = TimeSpan.FromSeconds(0).ToString();
                accumulatedPlayTime = (TimeSpan.Parse(accumulatedPlayTime) + OnTimeSpan).ToString();
                NeedsToSave = true;
                return accumulatedPlayTime;
            }
        }
        public byte TutorialState
        {
            get => tutorialProgress;
            set
            {
                if (tutorialProgress == value)
                    return;
                tutorialProgress = value;
                NeedsToSave = true;
            }
        }
        public byte Level
        {
            get => currentLevel;
            set
            {
                if (currentLevel == value)
                    return;
                currentLevel = value;
                NeedsToSave = true;
            }
        }
        public uint TotalExperience
        {
            get => experienceTotal;
            set
            {
                if (experienceTotal == value)
                    return;
                experienceTotal = value;
                NeedsToSave = true;
            }
        }
        public uint PEN
        {
            get => penBalance;
            set
            {
                if (penBalance == value)
                    return;
                penBalance = value;
                NeedsToSave = true;
            }
        }
        public uint AP
        {
            get => apPoints;
            set
            {
                if (apPoints == value)
                    return;
                apPoints = value;
                NeedsToSave = true;
            }
        }
        public uint Coins1
        {
            get => coinBalanceA;
            set
            {
                if (coinBalanceA == value)
                    return;
                coinBalanceA = value;
                NeedsToSave = true;
            }
        }
        public uint Coins2
        {
            get => coinBalanceB;
            set
            {
                if (coinBalanceB == value)
                    return;
                coinBalanceB = value;
                NeedsToSave = true;
            }
        }
        public uint TotalWins
        {
            get => victoryCount;
            set
            {
                if (victoryCount == value)
                    return;
                victoryCount = value;
                TotalMatches = victoryCount + defeatCount;
                NeedsToSave = true;
            }
        }
        public uint TotalLosses
        {
            get => defeatCount;
            set
            {
                if (defeatCount == value)
                    return;
                defeatCount = value;
                TotalMatches = victoryCount + defeatCount;
                NeedsToSave = true;
            }
        }
        public bool IsAllowToJoin
        {
            get => joinPermitted;
            set
            {
                if (joinPermitted == value)
                    return;
                joinPermitted = value;
                NeedsToSave = true;
            }
        }
        public uint NameTag
        {
            get => nameTagId;
            set
            {
                if (nameTagId == value)
                    return;
                nameTagId = value;
                NeedsToSave = true;
            }
        }
        public uint TotalMatches { get; private set; }
        #endregion
        public static bool operator !=(Player srcPlayer, Player destPlayer)
        {
            return srcPlayer?.Account?.Id != destPlayer?.Account?.Id;
        }
        public static bool operator ==(Player srcPlayer, Player destPlayer)
        {
            return srcPlayer?.Account?.Id == destPlayer?.Account?.Id;
        }
        public void Dispose()
        {
            if (Disposed || Session == null)
                return;
            GameServer.Instance?.PlayerManager?.Remove(this);
            Session?.Dispose();
            ChatSession?.Dispose();
            Session = null;
            ChatSession = null;
            Account = null;
            accumulatedPlayTime = null;
            tutorialProgress = 0;
            currentLevel = 0;
            experienceTotal = 0;
            penBalance = 0;
            apPoints = 0;
            coinBalanceA = 0;
            coinBalanceB = 0;
            TotalMatches = 0;
            defeatCount = 0;
            victoryCount = 0;
            nameTagId = 0;
            stats = null;
            Settings = null;
            DenyManager = null;
            FriendManager = null;
            Mailbox = null;
            Inventory = null;
            CharacterManager = null;
            ShoppingBasketManager = null;
            Club = null;
            Room = null;
            RoomInfo = null;
            DailyMission = null;
            IsChallengeReg = false;
            IsRankReg = false;
            IsAllowToJoin = false;
            EDHwid = "";
            Hwid = "";
            Disposed = true;
        }
        ~Player()
        {
            Dispose();
        }
    }
}

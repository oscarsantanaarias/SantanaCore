using System.Threading.Tasks;
using Santana.Database.Auth;

namespace Santana.Network
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using AuthServer.ServiceModel;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using SantanaLib.IO;
    using SantanaLib.Threading;
    using Dapper.FastCrud;
    using ExpressMapper;
    using ExpressMapper.Extensions;
    using Commands;
    using Database.Game;
    using Data.Chat;
    using Data.Club;
    using Data.Game;
    using Data.GameRule;
    using Message.Club;
    using Message.Game;
    using Message.GameRule;
    using Message.Relay;
    using Services;
    using Resource;
    using ProudNetSrc;
    using ProudNetSrc.Serialization;
    using Serilog;
    using Constants = Serilog.Core.Constants;
    using ErrorEventArgs = ProudNetSrc.ErrorEventArgs;
    using Santana.Network.Message.Chat;
    using System.Xml.Linq;

    internal class GameServer : ProudServer
    {
        private static readonly ILogger
            GameLog = Log.ForContext(Constants.SourceContextPropertyName, nameof(GameServer));

        private readonly ServerlistMgr _serverList;
        private readonly ILoop _tickLoop;
        private TimeSpan _persistElapsed;

        public static class CollectBookMapper
        {
            public static Dictionary<(int itemId, byte color), List<int>> Map = new();

            public static void Load(string path)
            {
                var doc = XDocument.Load(path);
                foreach (var book in doc.Descendants("collect_book"))
                {
                    int bookKey = int.Parse(book.Attribute("key").Value);
                    foreach (var collect in book.Elements("collect"))
                    {
                        int itemId = int.Parse(collect.Attribute("key").Value);
                        byte color = byte.Parse(collect.Attribute("color").Value);
                        var key = (itemId, color);
                        if (!Map.TryGetValue(key, out var list))
                        {
                            list = new List<int>();
                            Map[key] = list;
                        }
                        if (!list.Contains(bookKey))
                            list.Add(bookKey);
                    }
                }
            }

            public static List<int> GetBooks(int itemId, byte color)
            {
                return Map.TryGetValue((itemId, color), out var list)
                    ? list
                    : new List<int>();
            }
        }

        private GameServer(Configuration config)
            : base(config)
        {
            RegisterMappings();
            CommandManager = new CommandManager(this);
            CommandManager.Add(new ServerCommand())
    .Add(new ReloadCommand())
    .Add(new GameCommands())
    .Add(new BanCommands())
    .Add(new UnbanCommands())
    .Add(new UserkickCommand())
    .Add(new RoomkickCommand())
    .Add(new AdminCommands())
    .Add(new customCommands())
    .Add(new NoticeCommand())
    .Add(new CollectBookTestCommand())
    .Add(new RecreateCollectBookCommand())
    .Add(new CombiTestCommand())
    .Add(new OnlineCommand())
    .Add(new OfflineCommand())
    .Add(new LoginStateCommand())
    .Add(new ClubMemberDebugCommand())
    .Add(new PlayerInfoDebugCommand())
    .Add(new PlayerPositionDebugCommand())
    .Add(new PlayerInfoListDebugCommand())
    .Add(new CombiListDebugCommand())
    .Add(new PlayerLocationDebugCommand())
    .Add(new GMCommands())
    .Add(new WholeNoticeCommand())
    .Add(new SearchCommand())
    .Add(new CommandWrapper())
    .Add(new HelpCommand());
            PlayerManager = new PlayerManager();
            ResourceCache = new ResourceCache();
            ChannelManager = new ChannelManager(ResourceCache.GetChannels());
            ClubManager = new ClubManager(ResourceCache.GetClubs());
            _tickLoop = new ThreadLoop(TimeSpan.FromMilliseconds(100), Tick);
            _serverList = new ServerlistMgr();
        }

        public static GameServer Instance { get; private set; }
        public CommandManager CommandManager { get; }
        public PlayerManager PlayerManager { get; }
        public ChannelManager ChannelManager { get; }
        public ClubManager ClubManager { get; set; }
        public ResourceCache ResourceCache { get; }

        public static void Initialize(Configuration config)
        {
            if (Instance != null)
                throw new InvalidOperationException("Server is already initialized");
#if LATESTS4
            config.Version = new Guid("{14229beb-3338-7114-ab92-9b4af78c688f}");
#else
      config.Version = new Guid("{beb92241-8333-4117-ab92-9b4af78c688f}");
#endif
#if OLDUI
            config.Version = new Guid("{beb92241-8333-4117-ab92-9b4af78c688f}");
#endif
            config.MessageFactories = new MessageFactory[]
            {
                new RelayMessageFactory(), new GameMessageFactory(), new GameRuleMessageFactory(),
                new ClubMessageFactory()
            };
            config.SessionFactory = new GameSessionFactory();
            bool MustBeLoggedIn(GameSession session)
            {
                return session.IsLoggedIn();
            }
            bool MustNotBeLoggedIn(GameSession session)
            {
                return !session.IsLoggedIn();
            }
            bool MustBeInChannel(GameSession session)
            {
                return session.Player.Channel != null;
            }
            bool MustBeInRoom(GameSession session)
            {
                return session.Player.Room != null;
            }
            bool MustNotBeInRoom(GameSession session)
            {
                return session.Player.Room == null;
            }
            bool MustBeRoomHost(GameSession session)
            {
                return session.Player.Room.Host == session.Player;
            }
            bool MustBeRoomMaster(GameSession session)
            {
                return session.Player.Room.Master == session.Player;
            }
            config.MessageHandlers = new IMessageHandler[]
            {
                new MessageHandler<GameSession>()
                    .AddHandler(new AuthService())
                    .AddHandler(new CharService())
                    .AddHandler(new GeneralService())
                    .AddHandler(new AdminService())
                    .AddHandler(new ChannelService())
                    .AddHandler(new ShopService())
                    .AddHandler(new InventoryService())
                    .AddHandler(new RoomService())
                    .AddHandler(new ClubService())
                    .AddHandler(new UnionService())
                    .AddHandler(new UnusedService())
                    .RegisterRule<LoginRequestReqMessage>(MustNotBeLoggedIn)
                    .RegisterRule<CharacterCreateReqMessage>(MustBeLoggedIn)
                    .RegisterRule<CharacterSelectReqMessage>(MustBeLoggedIn)
                    .RegisterRule<CharacterDeleteReqMessage>(MustBeLoggedIn)
                    .RegisterRule<AdminShowWindowReqMessage>(MustBeLoggedIn)
                    .RegisterRule<AdminActionReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ChannelInfoReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ChannelEnterReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ChannelLeaveReqMessage>(MustBeLoggedIn, MustBeInChannel)
                    .RegisterRule<ItemBuyItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<RandomShopRollingStartReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ItemUseItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ItemRepairItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ItemRefundItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<ItemDiscardItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<NoteGiftItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<NoteImportuneItemReqMessage>(MustBeLoggedIn)
                    .RegisterRule<NoteGiftItemGainReqMessage>(MustBeLoggedIn)
                    .RegisterRule<RoomQuickJoinReqMessage>(MustBeLoggedIn, MustBeInChannel, MustNotBeInRoom)
                    .RegisterRule<RoomEnterPlayerReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<RoomMakeReqMessage>(MustBeLoggedIn, MustBeInChannel, MustNotBeInRoom)
                    .RegisterRule<RoomMakeReq2Message>(MustBeLoggedIn, MustBeInChannel, MustNotBeInRoom)
                    .RegisterRule<RoomEnterReqMessage>(MustBeLoggedIn, MustBeInChannel, MustNotBeInRoom)
                    .RegisterRule<RoomLeaveReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<RoomTeamChangeReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<RoomPlayModeChangeReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreAIKillReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreKillReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreKillAssistReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreOffenseReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreOffenseAssistReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreDefenseReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreDefenseAssistReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreTeamKillReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreHealAssistReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreSuicideReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ArcadeStageInfoReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ScoreReboundReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom, MustBeRoomHost,
                        session => session.Player.RoomInfo.State != PlayerState.Lobby &&
                                   session.Player.RoomInfo.State != PlayerState.Spectating)
                    .RegisterRule<ScoreGoalReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        session => session.Player.RoomInfo.State != PlayerState.Lobby &&
                                   session.Player.RoomInfo.State != PlayerState.Spectating)
                    .RegisterRule<SeizePositionCaptureReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        session => session.Player.RoomInfo.State != PlayerState.Lobby &&
                                   session.Player.RoomInfo.State != PlayerState.Spectating)
                    .RegisterRule<SeizeBuffItemGainReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        session => session.Player.RoomInfo.State != PlayerState.Lobby &&
                                   session.Player.RoomInfo.State != PlayerState.Spectating)
                    .RegisterRule<RoomBeginRoundReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        MustBeRoomMaster)
                    .RegisterRule<RoomReadyRoundReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        session => session.Player.RoomInfo.State == PlayerState.Lobby)
                    .RegisterRule<RoomBeginRoundReq2Message>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        MustBeRoomMaster)
                    .RegisterRule<GameLoadingSuccessReqMessage>(MustBeLoggedIn, MustBeInChannel)
                    .RegisterRule<RoomReadyRoundReq2Message>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        session => session.Player.RoomInfo.State == PlayerState.Lobby)
                    .RegisterRule<GameEventMessageReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<RoomItemChangeReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                                            session => session.Player.RoomInfo.State == PlayerState.Lobby)
                    .RegisterRule<GameAvatarChangeReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                      session => session.Player.RoomInfo.State == PlayerState.Lobby ||
                                   session.Player.Room.GameRuleManager.GameRule.StateMachine.IsInState(
                                       GameRuleState.HalfTime))
                    .RegisterRule<RoomChangeRuleNotifyReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        MustBeRoomMaster,
                        session =>
                            session.Player.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting))
                    .RegisterRule<RoomChangeRuleNotifyReq2Message>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,
                        MustBeRoomMaster,
                        session =>
                            session.Player.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting))
                    .RegisterRule<ClubAddressReqMessage>(MustBeLoggedIn, MustBeInChannel)
                    .RegisterRule<UnionMainUiReqMessage>(MustBeLoggedIn, MustBeInChannel)
                    .RegisterRule<UnionSearchRoomReqMessage>(MustBeLoggedIn, MustBeInChannel)
                    .RegisterRule<RoomLeaveReguestReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom)
                    .RegisterRule<ClubCreateReqMessage>(MustBeLoggedIn)
                    .RegisterRule<PromotionCointEventGetCoinReqMessage>(MustBeLoggedIn, MustBeInChannel, MustBeInRoom,                         session =>
                            session.Player.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing))
            };
#if DEBUG
      config.Logger = GameLog;
#endif
            Instance = new GameServer(config);
        }

        public void BroadcastNotice(string message)
        {
            Broadcast(new NoticeAdminMessageAckMessage(message));
        }

        private void Tick(TimeSpan delta)
        {
            try
            {
                ChannelManager.Update(delta);
            }
            catch (Exception channelUpdateError)
            {
                GameLog.Error(channelUpdateError.ToString());
            }
            Parallel.ForEach(PlayerManager, (member) =>
            {
                try
                {
                    if (member.Session == null && !member.NeedsToSave)
                        member.Dispose();
                }
                catch (Exception disposeError)
                {
                    GameLog.Error(disposeError.ToString());
                }
            });
            _persistElapsed = _persistElapsed.Add(delta);
            if (_persistElapsed <= Config.Instance.SaveInterval)
                return;
            _persistElapsed = TimeSpan.Zero;
            var onlinePlayers = PlayerManager.Where(candidate => candidate.IsLoggedIn());
            var snapshot = onlinePlayers as Player[] ?? onlinePlayers.ToArray();
            if (!snapshot.Any())
                return;
            foreach (var member in snapshot)
            {
                try
                {
                    member.Save();
                }
                catch (Exception saveError)
                {
                    GameLog.ForAccount(member)
                        .Error(saveError, "Persisting this account's profile to storage did not complete");
                }
            }
        }

        private static void RegisterMappings()
        {
            Mapper.Register<GameServer, ServerInfoDto>()
                .Member(dest => dest.ApiKey, src => Config.Instance.AuthAPI.ApiKey)
                .Member(dest => dest.Id, src => Config.Instance.Id)
                .Member(dest => dest.Name,
                    src => $"{Config.Instance.Name}")
          .Member(dest => dest.PlayerLimit, src => Config.Instance.PlayerLimit)
          .Member(dest => dest.PlayerOnline, src => src.Sessions.Count)
          .Member(dest => dest.EndPoint,
              src => new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.Listener.Port))
          .Member(dest => dest.ChatEndPoint,
              src => new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.ChatListener.Port));
            Mapper.Register<Player, PlayerAccountInfoDto>()
                .Function(dest => dest.IsGM, src => src.Account.SecurityLevel > SecurityLevel.Tester)
                .Member(dest => dest.Level, src => src.Level)
                .Member(dest => dest.GameTime, src => TimeSpan.Parse(src.PlayTime))
                .Member(dest => dest.TotalExp, src => src.TotalExperience)
                .Function(dest => dest.CombiMasterExp, src => src.GetCombiMasterExp())
                .Function(dest => dest.TutorialState,
                    src => (uint)(Config.Instance.Game.EnableTutorial ? src.TutorialState : 1))
                .Member(dest => dest.Nickname, src => src.Account.Nickname)
                .Member(dest => dest.TotalMatches, src => src.TotalLosses + src.TotalWins)
                .Member(dest => dest.MatchesWon, src => src.TotalWins)
                .Member(dest => dest.MatchesLost, src => src.TotalLosses)
                .Member(dest => dest.BRStats, src => src.stats.BattleRoyal.GetStatsDto())
                .Member(dest => dest.ChaserStats, src => src.stats.Chaser.GetStatsDto())
                .Member(dest => dest.CPTStats, src => src.stats.Captain.GetStatsDto())
                .Member(dest => dest.DMStats, src => src.stats.DeathMatch.GetStatsDto())
                .Member(dest => dest.TDStats, src => src.stats.TouchDown.GetStatsDto())
                .Member(dest => dest.SiegeStats, src => src.stats.Siege.GetStatsDto())
                .Member(dest => dest.ArenaStats, src => src.stats.Arena.GetStatsDto());
            Mapper.Register<Channel, ChannelInfoDto>()
                .Member(dest => dest.PlayersOnline, src => src.Players.Count);
            Mapper.Register<PlayerItem, ItemDto>()
                .Member(dest => dest.Id, src => src.Id)
                .Member(dest => dest.EnchantLevel, src => src.EnchantLvl)
                .Function(dest => dest.ExpireTime, src => src.CalculateExpireTime())
                .Function(dest => dest.Durability, src =>
                {
                    if (src.PeriodType == ItemPeriodType.Units) return (int)src.Count;
                    return src.Durability;
                })
                .Function(dest => dest.Effects, src =>
                {
                    var effectList = new List<ItemEffectDto>();
                    if (src is null)
                        return effectList.ToArray();
                    src.Effects.ToList().ForEach(fx => { effectList.Add(new ItemEffectDto { Effect = fx }); });
                    return effectList.ToArray();
                });
            Mapper.Register<Deny, DenyDto>()
                .Member(dest => dest.AccountId, src => src.DenyId)
                .Member(dest => dest.Nickname, src => src.Nickname);
            Mapper.Register<PlayerItem, Data.P2P.ItemDto>()
                .Function(dest => dest.ItemNumber, src => src?.ItemNumber ?? 0);
            Mapper.Register<RoomCreationOptions, ChangeRuleDto>()
                .Function(dest => dest.GameRule, src => src.GameRule)
                .Member(dest => dest.MapId, src => (byte)src.MapId)
                .Member(dest => dest.PlayerLimit, src => src.PlayerLimit)
                .Member(dest => dest.Points, src => src.ScoreLimit)
                .Member(dest => dest.Time, src => (byte)src.TimeLimit.TotalMinutes)
                .Member(dest => dest.ItemLimit, src => src.ItemLimit)
                .Member(dest => dest.Password, src => src.Password)
                .Member(dest => dest.Name, src => src.Name)
                .Member(dest => dest.HasSpectator, src => src.HasSpectator)
                .Member(dest => dest.SpectatorLimit, src => src.SpectatorLimit);
            Mapper.Register<RoomCreationOptions, ChangeRuleDto2>()
                .Function(dest => dest.GameRule, src => src.GameRule)
                .Member(dest => dest.MapId, src => (byte)src.MapId)
                .Member(dest => dest.PlayerLimit, src => src.PlayerLimit)
                .Member(dest => dest.Points, src => src.ScoreLimit)
                .Value(dest => dest.Unk1, 0)
                .Member(dest => dest.Time, src => (byte)src.TimeLimit.TotalMinutes)
                .Member(dest => dest.ItemLimit, src => src.ItemLimit)
                .Member(dest => dest.Password, src => src.Password)
                .Member(dest => dest.Name, src => src.Name)
                .Member(dest => dest.HasSpectator, src => src.HasSpectator)
                .Member(dest => dest.SpectatorLimit, src => src.SpectatorLimit)
                .Member(dest => dest.ChangeRuleId, src => src.ChangeRuleId)
                .Function(dest => dest.IsRandom, src => src.IsRandom ? 1 : 0)
                .Member(dest => dest.Unk3, src => src.Unk3)
                .Member(dest => dest.FMBurnMode, src => src.GetFMBurnModeInfo());
            Mapper.Register<Mail, NoteDto>()
                .Member(dest => dest.Id, src => src.Id)
                .Function(dest => dest.Unk1, src => (ulong)(src.Id & 0xFFFFFFFF))
                .Function(dest => dest.Receiver, src => (ulong)(src.Id >> 32))
                .Member(dest => dest.Title, src => src.Title)
                .Function(dest => dest.ReadCount, src => src.IsNew ? 0 : 1)
                .Function(dest => dest.DaysLeft,
                    src => DateTimeOffset.Now < src.Expires
                        ? (byte)System.Math.Max(0, System.Math.Min(255,
                            (int)(src.Expires - DateTimeOffset.Now).TotalDays))
                        : (byte)0)
                .Function(dest => dest.MessageType, src => src.MessageType)
                .Function(dest => dest.Unk8, src => (byte)(src.OpenedGift ? 1 : 0))
                .Function(dest => dest.Sender, src => src.IsClan ? string.Empty : src.Sender);
            Mapper.Register<Mail, NoteContentDto>()
                .Function(dest => dest.Unk1, src => src.IsGift || src.IsRequest ? src.Id : 0UL)
                .Member(dest => dest.Message, src => src.Message)
                .Function(dest => dest.Item, src => new NoteItemDto
                {
                    Unk0 = src.IsGift || src.IsRequest ? (int)(uint)src.Gift.ItemNumber : 0,
                    Unk1 = src.IsGift || src.IsRequest ? (int)src.Gift.PriceType : 0,
                    Unk2 = src.IsGift || src.IsRequest ? (int)src.Gift.PeriodType : 0,
                    Unk3 = src.IsGift || src.IsRequest ? (short)src.Gift.Period : (short)0,
                    Unk4 = src.IsGift || src.IsRequest ? src.Gift.Color : (byte)0,
                    Unk5 = src.IsGift || src.IsRequest ? src.Gift.Flags : 0
                })
                .Function(dest => dest.Unk2, src => src.IsGift || src.IsRequest ? src.Gift.Mode : (byte)0)
                .Function(dest => dest.Unk3, src => src.IsGift && src.OpenedGift ? (byte)1 : (byte)0);
            Mapper.Register<PlayerItem, ItemDurabilityInfoDto>()
                .Member(dest => dest.ItemId, src => src.Id)
                .Function(dest => dest.Durabilityloss, src =>
                {
                    var pendingLoss = src.DurabilityLoss;
                    src.DurabilityLoss = 0;
                    return pendingLoss;
                });
            Mapper.Register<Player, PlayerInfoShortDto>()
                .Function(dest => dest.AccountId, src => src?.Account?.Id ?? 0)
                .Function(dest => dest.Nickname, src => src?.Account?.Nickname ?? "n/A")
                .Function(dest => dest.IsGM, src => src?.Account?.SecurityLevel > SecurityLevel.Tester)
                .Function(dest => dest.TotalExp, src => src?.TotalExperience ?? 0);
            Mapper.Register<Player, PlayerLocationDto>()
                .Function(dest => dest.ChannelId, src => src.Channel?.Id > 0 ? (int)src?.Channel?.Id : -1)
                .Function(dest => dest.RoomId, src => src.Room?.Id > 0 ? (int)src?.Room?.Id : -1)
                .Function(dest => dest.Unk, src => Instance.PlayerManager.Contains(src.Account.Id) ? -2 : -1)
                .Function(dest => dest.ServerGroupId,
                    src => Instance.PlayerManager.Contains(src.Account.Id) ? Config.Instance.Id : -1)
                .Function(dest => dest.GameServerId,
                    src => Instance.PlayerManager.Contains(src.Account.Id) ? Math.Max(2, (int)Config.Instance.Id) : -1)
                .Function(dest => dest.ChatServerId,
                    src => Instance.PlayerManager.Contains(src.Account.Id) ? Math.Max(1, (int)Config.Instance.Id) : -1);
            Mapper.Register<Player, PlayerInfoDto>()
                .Function(dest => dest.Info, src => src.Map<Player, PlayerInfoShortDto>())
                .Function(dest => dest.Location, src => src.Map<Player, PlayerLocationDto>());
            Mapper.Register<Player, PlayerNameTagInfoDto>()
                .Function(dest => dest.AccountId, src => src.Account?.Id ?? 0)
                .Function(dest => dest.TagId, src => src.NameTag)
                .Function(dest => dest.Unk1, src => 0u);
            Mapper.Register<Player, NameTagDto>()
              .Function(dest => dest.AccountId, src => src.Account?.Id ?? 0)
              .Function(dest => dest.TagId, src => src.NameTag);
            Mapper.Register<Player, UserDataDto>()
               .Function(dest => dest.Nickname, src => src.Account?.Nickname ?? "")
               .Function(dest => dest.AccountId, src => src.Account?.Id ?? 0)
               .Member(dest => dest.TotalExp, src => src.TotalExperience)
               .Member(dest => dest.ClanId, src => (int)src.Club.Id)
               .Member(dest => dest.ClanIcon, src => src.Club.ClanIcon ?? string.Empty)
               .Member(dest => dest.ClanName, src => src.Club.ClanName ?? string.Empty)
               .Function(dest => dest.Level, src => src.Level)
               .Member(dest => dest.GameTime, src => TimeSpan.Parse(src.PlayTime))
               .Member(dest => dest.TotalMatches, src => src.TotalMatches)
               .Member(dest => dest.MatchesWon, src => src.TotalWins)
               .Member(dest => dest.MatchesLost, src => src.TotalLosses)
               .Member(dest => dest.Unk4, src => 3)
               .Member(dest => dest.Unk5, src => 3)
               .Member(dest => dest.Unk6, src => 3)
               .Member(dest => dest.Unk7, src => 0f)
               .Member(dest => dest.TDStats, src => src.stats.TouchDown.GetUserDataDto())
               .Member(dest => dest.TDScore, src => src.stats.TouchDown.GetUserDataScoreDto())
               .Member(dest => dest.DMStats, src => src.stats.DeathMatch.GetUserDataDto())
               .Member(dest => dest.DMScore, src => src.stats.DeathMatch.GetUserDataScoreDto())
               .Member(dest => dest.ChaserStats, src => src.stats.Chaser.GetUserDataDto())
               .Member(dest => dest.ChaserSurvivability, src => src.stats.Chaser.GetUserDataScoreDto())
               .Member(dest => dest.CaptainStats, src => src.stats.Captain.GetUserDataDto())
               .Member(dest => dest.CaptainScore, src => src.stats.Captain.GetUserDataScoreDto())
               .Member(dest => dest.BattleRoyalStats, src => src.stats.BattleRoyal.GetUserDataDto())
               .Member(dest => dest.BRScore, src => src.stats.BattleRoyal.BRScore)
               .Member(dest => dest.SiegeStats, src => src.stats.Siege.GetUserDataDto())
               .Member(dest => dest.SiegeScore, src => src.stats.Siege.GetUserDataScore())
               .Member(dest => dest.ArenaStats, src => src.stats.Arena.GetUserDataDto())
               .Member(dest => dest.ArenaScore, src => src.stats.Arena.GetUserDataScore())
               .Member(dest => dest.Gender, src => src.CharacterManager.CurrentCharacter.Gender)
               .Member(dest => dest.Clothes, src => src.ItemStats.Clothes.GetUserDataDto())
               .Member(dest => dest.Weapons, src => src.ItemStats.Weapons.GetUserDataDto())
               .Member(dest => dest.Skills, src => src.ItemStats.Skill.GetUserDataDto());
            Mapper.Register<Player, ClubMyInfoDto>()
                .Function(dest => dest.Id, src => src.Club?.Id ?? 0)
                .Function(dest => dest.Name, src => src.Club?.ClanName ?? string.Empty)
                .Function(dest => dest.Rank, src => src.Club?.GetPlayer(src.Account.Id)?.Rank ?? ClubRank.Member)
                .Function(dest => dest.Type, src => src.Club?.ClanIcon ?? string.Empty)
                .Function(dest => dest.State, src => src.Club?[src.Account?.Id ?? 0].State ?? 0);
            Mapper.Register<Player, PlayerClubInfoDto>()
                .Function(dest => dest.Id, src => src.Club?.Id ?? 0)
                .Function(dest => dest.Name, src => src.Club?.ClanName ?? string.Empty)
                .Function(dest => dest.Type, src => src.Club?.ClanIcon ?? string.Empty);
            Mapper.Register<Player, ClubMemberDto2>()
                .Function(dest => dest.AccountId, src => src.Account?.Id ?? 0)
                .Function(dest => dest.Nickname, src => src.Account?.Nickname ?? "n/A")
                .Function(dest => dest.ServerId, src => Instance.PlayerManager.Contains(src.Account.Id) ? Config.Instance.Id : -1)
                .Function(dest => dest.ChannelId, src => src.Channel?.Id > 0 ? src.Channel.Id : -1)
                .Function(dest => dest.RoomId, src => src.Room?.Id > 0 ? (int)src.Room.Id : -1)
                .Function(dest => dest.ClanRank, src => src.Club?.GetPlayer(src.Account.Id)?.Rank ?? ClubRank.Member)
                .Function(dest => dest.LastLogin, src => src.Account?.AccountDto?.LastLogin ?? string.Empty);
            Mapper.Register<ClubPlayerInfo, ClubMemberDto2>()
                .Function(dest => dest.AccountId, src => src.AccountId)
                .Function(dest => dest.Nickname, src => src.Account?.Nickname ?? "n/A")
                .Function(dest => dest.LastLogin, src => src.Account.LastLogin ?? string.Empty)
                .Function(dest => dest.ClanRank, src => src.Rank)
                .Value(dest => dest.ServerId, -1)
                .Value(dest => dest.ChannelId, -1)
                .Value(dest => dest.RoomId, -1);
            Mapper.Register<Player, ClubMemberDto>()
                .Function(dest => dest.AccountId, src => src.Account?.Id ?? 0)
                .Function(dest => dest.Nickname, src => src.Account?.Nickname ?? "n/A")
                .Function(dest => dest.ClanRank, src => src.Club?.GetPlayer(src.Account.Id)?.Rank ?? ClubRank.Member)
                .Function(dest => dest.LastLogin, src => src.Account?.AccountDto?.LastLogin ?? string.Empty);
            Mapper.Register<ClubPlayerInfo, ClubMemberDto>()
                .Function(dest => dest.AccountId, src => src.AccountId)
                .Function(dest => dest.Nickname, src => src.Account?.Nickname ?? "n/A")
                .Function(dest => dest.LastLogin, src => src.Account.LastLogin ?? string.Empty)
                .Function(dest => dest.ClanRank, src => src.Rank);
            Mapper.Register<Player, ClubInfoDto>()
                .Function(dest => dest.Id, src => src.Club?.Id ?? 0)
                .Function(dest => dest.Name, src => src.Club?.ClanName ?? "n/A")
                .Function(dest => dest.MasterName,
                    src => src.Club?.Players.Values.FirstOrDefault(x => x.Rank == ClubRank.Master)?.Account?.Nickname ??
                           string.Empty)
                .Function(dest => dest.MemberCount, src => src.Club?.Count + 5 ?? 0)
                .Function(dest => dest.Type, src => src.Club?.ClanIcon ?? string.Empty)
                .Function(dest => dest.Unk1, src => (int)(src.Club?.ClubPoints ?? 0))
                .Function(dest => dest.Unk2, src => (int)(src.Club?.ClanRank ?? 0))
                .Function(dest => dest.Unk3, src => (int)(src.Club?.ClubWin ?? 0))
                .Function(dest => dest.Unk4, src => (int)(src.Club?.ClubLoss ?? 0));
            Mapper.Register<Player, ClubInfoDto2>()
                .Function(dest => dest.Id, src => src.Club?.Id ?? 0)
                .Function(dest => dest.Id2, src => src.Club?.Id ?? 0)
                .Function(dest => dest.Name, src => src.Club?.ClanName ?? "n/A")
                .Function(dest => dest.MasterName,
                    src => src.Club?.Players.Values.FirstOrDefault(x => x.Rank == ClubRank.Master)?.Account?.Nickname ??
                           string.Empty)
                .Function(dest => dest.MemberCount, src => src.Club?.Count + 5 ?? 0)
                .Function(dest => dest.Type, src => src.Club?.ClanIcon ?? string.Empty)
                .Function(dest => dest.Unk4, src => src.Club?.ClanRank ?? 0u)
                .Function(dest => dest.Unk5, src => src.Club?.ClubPoints ?? 0)
                .Function(dest => dest.Unk7, src => src.Club?.ClubWin ?? 0)
                .Function(dest => dest.Unk8, src => src.Club?.ClubLoss ?? 0);
            Mapper.Register<Friend, PlayerInfoDto>()
                .Function(dest => dest.Info, src =>
                {
                    var onlinePlayer = Instance.PlayerManager.Get(src.FriendId);
                    if (onlinePlayer != null)
                    {
                        return onlinePlayer.Map<Player, PlayerInfoShortDto>();
                    }
                    using (var accountDb = AuthDatabase.Open())
                    using (var gameDb = GameDatabase.Open())
                    {
                        var accountRow = DbUtil.Find<AccountDto>(accountDb, statement => statement
                            .Where($"{nameof(AccountDto.Id):C} = @Id")
                            .WithParameters(new { Id = src.FriendId })).FirstOrDefault();
                        var playerRow = DbUtil.Find<PlayerDto>(gameDb, statement => statement
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                            .WithParameters(new { Id = src.FriendId })).FirstOrDefault();
                        if (playerRow != null && accountRow != null)
                        {
                            return new PlayerInfoShortDto(src.FriendId, accountRow.Nickname,
                                playerRow.TotalExperience,
                                (SecurityLevel)accountRow.SecurityLevel >= SecurityLevel.GameSage);
                        }
                        if (accountRow != null)
                        {
                            return new PlayerInfoShortDto(src.FriendId, accountRow.Username, 0,
                                ((SecurityLevel)accountRow.SecurityLevel) >= SecurityLevel.GameSage);
                        }
                        return new PlayerInfoShortDto(0, string.Empty, 0, false);
                    }
                })
                .Function(dest => dest.Location, src =>
                {
                    var onlinePlayer = Instance.PlayerManager.Get(src.FriendId);
                    return onlinePlayer?.Map<Player, PlayerLocationDto>() ?? new PlayerLocationDto();
                });
            Mapper.Register<ClubPlayerInfo, PlayerInfoDto>()
                .Function(dest => dest.Info, src =>
                {
                    var onlinePlayer = Instance.PlayerManager.Get(src.AccountId);
                    if (onlinePlayer != null)
                    {
                        return onlinePlayer.Map<Player, PlayerInfoShortDto>();
                    }
                    using (var gameDb = GameDatabase.Open())
                    {
                        var playerRow = DbUtil.Find<PlayerDto>(gameDb, statement => statement
                            .Where($"{nameof(PlayerDto.Id):C} = @Id")
                            .WithParameters(new { Id = src.AccountId })).FirstOrDefault();
                        if (playerRow != null)
                        {
                            return new PlayerInfoShortDto(src.AccountId, src.Account?.Nickname ?? string.Empty,
                                playerRow.TotalExperience,
                                (SecurityLevel)src.Account.SecurityLevel >= SecurityLevel.GameSage);
                        }
                        return new PlayerInfoShortDto(src.AccountId, src.Account?.Nickname ?? string.Empty, 0,
                            ((SecurityLevel)(src.Account?.SecurityLevel ?? 0)) >= SecurityLevel.GameSage);
                    }
                })
                .Function(dest => dest.Location, src =>
                {
                    var onlinePlayer = Instance.PlayerManager.Get(src.AccountId);
                    return onlinePlayer?.Map<Player, PlayerLocationDto>() ?? new PlayerLocationDto();
                });
            Mapper.Register<ShoppingBasketDto, ShoppingBasketDto>()
                .Member(dest => dest.ItemId, src => src.ItemId)
                .Member(dest => dest.ShopItem, src => src.ShopItem);
            Mapper.Compile(CompilationTypes.Source);
        }

        #region Events
        protected override void OnStarted()
        {
            ResourceCache.PreCache();
            _tickLoop.Start();
            _serverList.Start();
        }

        protected override void OnStopping()
        {
            _tickLoop.Stop(new TimeSpan(0));
            _serverList.Dispose();
        }

        protected override void OnDisconnected(ProudSession session)
        {
            try
            {
                var leaving = (GameSession)session;
                if (leaving.Player != null)
                {
                    leaving.Player.Room?.Leave(leaving.Player);
                    leaving.Player.Channel?.Leave(leaving.Player);
                    leaving.Player.Save();
                    PlayerManager.Remove(leaving.Player);
                    GameLog.ForAccount(leaving.Player)
                        .Information($"Peer at {session.RemoteEndPoint} dropped its link and was torn down");
                    if (leaving.Player.ChatSession != null)
                    {
                        Club.LogOff(leaving.Player);
                        leaving.Player.ChatSession.GameSession = null;
                        leaving.Player.ChatSession.Dispose();
                    }
                    Services.IpcService.NotifyPlayerDisconnected(leaving.Player.Account.Id);
                    ShopService.OnPlayerLogout(leaving.Player.Account.Id);
                    try
                    {
                        using (var authDb = AuthDatabase.Open())
                        {
                            var accountRow = DbUtil.Find<AccountDto>(authDb, statement => statement
                                     .Include<BanDto>(join => join.LeftOuterJoin())
                                     .Where($"{nameof(AccountDto.Id):C} = @Id")
                                     .WithParameters(new { Id = leaving.Player.Account.Id })).FirstOrDefault();
                            if (accountRow != null)
                            {
                                accountRow.Status = 0;
                                DbUtil.Update(authDb, accountRow);
                            }
                        }
                    }
                    catch { }
                    leaving.Player.Session = null;
                    leaving.Player.ChatSession = null;
                    leaving.Player.Dispose();
                    leaving.Player = null;
                }
            }
            catch (Exception disconnectError)
            {
                GameLog.Error(disconnectError.ToString());
            }
            base.OnDisconnected(session);
        }

        protected override void OnError(ErrorEventArgs e)
        {
            var faulted = (GameSession)e.Session;
            var log = GameLog;
            if (e.Session != null)
                log = log.ForAccount((GameSession)e.Session);
            var text = e.Exception.ToString().ToLower();
            if (text.Contains("opcode") ||
                text.Contains("rmi") ||
                text.Contains("bad format in"))
            {
                log.Warning(e.Exception, "Wire frame did not decode into a known request: {msg}",
                    e.Exception.InnerException?.Message ?? e.Exception.Message);
                if (faulted?.Player?.Room == null || !faulted.Player.RoomInfo.HasLoaded)
                    faulted?.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
            }
            else
            {
                log.Error(e.Exception, "Session processing aborted on an uncategorized fault");
                if (faulted?.Player?.Room == null || !faulted.Player.RoomInfo.HasLoaded)
                    faulted?.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
            }
            base.OnError(e);
        }
        #endregion
    }
}

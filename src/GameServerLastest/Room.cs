using Santana.Game;
namespace Santana
{
    using SantanaLib.Collections.Concurrent;
    using SantanaLib.Threading.Tasks;
    using ExpressMapper.Extensions;
    using ProudNetSrc;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Data.Chat;
    using Santana.Network.Data.Club;
    using Santana.Network.Data.Game;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Club;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using Santana.Network.Services;
    using Serilog;
    using Serilog.Core;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Threading.Tasks;
    using Santana.Network.Message.Event;
    using Microsoft.Extensions.Options;
    internal class Room : IDisposable
    {
        public static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, "GameRoomMgr");
        internal static byte NormalizeArcadePlayerLimitFromClient(byte clientValue)
        {
            if (clientValue <= 1)
                return (byte)(clientValue + 1);
            return (byte)Math.Clamp((int)clientValue, 2, 4);
        }
        internal static byte ArcadePlayerLimitToClient(byte storedValue) =>
            storedValue switch
            {
                1 => 0,
                2 => 1,
                _ => storedValue
            };
        internal byte GetWirePlayerLimit() =>
            Options.GameRule == GameRule.Arcade
                ? ArcadePlayerLimitToClient(Options.PlayerLimit)
                : Options.PlayerLimit;
        internal static bool IsArcadeRoom(GameRule current, GameRule requested) =>
            current == GameRule.Arcade || requested == GameRule.Arcade;
        private TimeSpan _changingRulesTime = TimeSpan.FromSeconds(2);
        private TimeSpan _hostUpdateTime = TimeSpan.FromSeconds(30);
        private TimeSpan _voteKickTime = TimeSpan.FromSeconds(10);
        private ConcurrentDictionary<ulong, object> _kickedPlayers = new ConcurrentDictionary<ulong, object>();
        private ConcurrentDictionary<ulong, Player> _players = new ConcurrentDictionary<ulong, Player>();
        public ConcurrentDictionary<Player, Team> _blockplayers = new ConcurrentDictionary<Player, Team>();
        private Dictionary<Player, PlayerGameMode> _roomChangePlayers = new Dictionary<Player, PlayerGameMode>();
        private Dictionary<Player, PlayerGameMode> _roomChangeAlphaPlayers = new Dictionary<Player, PlayerGameMode>();
        private Dictionary<Player, PlayerGameMode> _roomChangeBetaPlayers = new Dictionary<Player, PlayerGameMode>();
        public AsyncLock _playerSync = new AsyncLock();
        private TimeSpan _changingRulesTimer;
        private TimeSpan _voteKicktimer;
        public bool Disposed { get; private set; }
        public Room(RoomManager roomManager, uint id, RoomCreationOptions options, Player creator)
        {
            RoomManager = roomManager;
            Id = id;
            Options = options;
            TimeCreated = DateTime.Now;
            TeamManager = new TeamManager(this);
            GameRuleManager = new GameRuleManager(this);
            VoteKickMgr = new VoteKickManager(this);
            Creator = creator;
            Master = creator;
            TeamManager.TeamChanged += TeamManager_TeamChanged;
            GameRuleManager.GameRuleChanged += GameRuleManager_OnGameRuleChanged;
            GameRuleManager.MapInfo = GameServer.Instance.ResourceCache.GetMaps()[options.MapId];
            GameRuleManager.GameRule = RoomManager.GameRuleFactory.Get(Options.GameRule, this);
        }
        public void Dispose()
        {
            if (Disposed || _playerSync == null)
                return;
            Disposed = true;
            Id = 0;
            foreach (var plr in Players.Values)
            {
                if (plr == null)
                    continue;
                try
                {
                    Leave(plr);
                }
                catch { }
            }
            _playerSync.Lock().Dispose();
            _playerSync = null;
            _roomChangePlayers.Clear();
            _roomChangeAlphaPlayers.Clear();
            _roomChangeBetaPlayers.Clear();
            _kickedPlayers.Clear();
            _players.Clear();
            TeamManager.TeamChanged -= TeamManager_TeamChanged;
            GameRuleManager.GameRuleChanged -= GameRuleManager_OnGameRuleChanged;
            GameRuleManager.MapInfo = null;
            GameRuleManager.GameRule = null;
            RoomManager = null;
            Options = null;
            TimeCreated = DateTime.Now;
            TeamManager = null;
            GameRuleManager = null;
            VoteKickMgr = null;
            Creator = null;
            Master = null;
            Host = null;
            HasStarted = false;
            IsPreparing = false;
        }
        public RoomManager RoomManager { get; private set; }
        public uint Id { get; private set; }
        public RoomCreationOptions Options { get; private set; }
        public DateTime TimeCreated { get; private set; }
        public TeamManager TeamManager { get; private set; }
        public GameRuleManager GameRuleManager { get; private set; }
        public bool HasStarted { get; set; }
        public bool IsPreparing { get; set; }
        public bool HasSurrender { get; set; }
        public GameState GameState { get; set; } = GameState.Waiting;
        public GameTimeState SubGameState { get; set; }
        public GameRuleState GameRuleState => GameRuleManager.GameRule.StateMachine.State;
        public TimeSpan RoundTime { get; set; } = TimeSpan.Zero;
        public IReadOnlyDictionary<ulong, Player> Players => _players;
        public Player Master { get; private set; }
        public Player Host { get; private set; }
        public Player Creator { get; private set; }
        public VoteKickManager VoteKickMgr { get; private set; }
        public bool IsChangingRules { get; private set; }
        private bool IsChangingRulesCooldown { get; set; }
        public void Update(TimeSpan delta)
        {
            if (Disposed)
                return;
            try
            {
                if (!Players.Any() || Players.Count() <= 0)
                {
                    RoomManager.Remove(this);
                    return;
                }
                if (!(Master?.IsLoggedIn() ?? true) || Master?.Room != this)
                {
                    if (TeamManager.Players.Any())
                    {
                        ChangeMasterIfNeeded(GetPlayerWithLowestPing(), true);
                        ChangeHostIfNeeded(GetPlayerWithLowestPing(), true);
                    }
                }
                if (!TeamManager.NoSpectatorPlayers.Any() && TeamManager.Players.Any())
                {
                    foreach (var spectator in TeamManager.Spectators)
                    {
                        TeamManager.ChangeMode(spectator, PlayerGameMode.Normal);
                    }
                }
                if (IsChangingRules)
                {
                    _changingRulesTimer += delta;
                    if (_changingRulesTimer >= _changingRulesTime && !IsChangingRulesCooldown)
                    {
                        RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
                        Broadcast(new RoomChangeRuleAckMessage(Options.Map<RoomCreationOptions, ChangeRuleDto2>()));
                        Broadcast(new GameChangeStateAckMessage(GameState));
                        IsChangingRulesCooldown = true;
                    }
                    foreach (var player in _players.Values)
                        player.RoomInfo.LastMapID = (byte)Options.MapId;
                    if (_changingRulesTimer >= _changingRulesTime.Add(TimeSpan.FromSeconds(3)))
                    {
                        IsChangingRules = false;
                        IsChangingRulesCooldown = false;
                        foreach (var player in _players.Values.Where(x => x.RoomInfo.IsReady))
                        {
                            player.RoomInfo.IsReady = false;
                            player.Room.Broadcast(new RoomReadyRoundAckMessage(player.Account.Id, player.RoomInfo.IsReady));
                        }
                    }
                }
                else
                {
                    foreach (var player in Players.Values.Where(x => !TeamManager.Players.Contains(x)))
                    {
                        TeamManager.Join(player);
                    }
                }
                if (VoteKickMgr.State == VoteKickManager.KickState.Execution)
                {
                    _voteKicktimer += delta;
                    if (_voteKicktimer < _voteKickTime)
                    {
                        VoteKickMgr.Update();
                    }
                    else
                    {
                        _voteKicktimer = TimeSpan.Zero;
                        VoteKickMgr.Evaluate();
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e.ToString());
            }
            GameRuleManager?.Update(delta);
        }
        public void Join(Player plr)
         {
                 if (Disposed)
                     return;
                 if (plr == null)
                 {
                      throw new RoomException("Player not exists");
            }
            if (plr.Room != null)
                 {
                      throw new RoomException("Player is already inside a room");
                 }
            if (_players.ContainsKey(plr.Account.Id))
                 {
                      throw new RoomException("Player is already inside a room");
                 }
                 if (Options.IsNoIntrusion && GameState != GameState.Waiting)
                 {
                      throw new RoomLimitIsNoIntrutionException();
                 }
                 var clubBattleTeam = ResolveClubBattleTeam(plr);
                 if (!CanJoinClubBattle(plr, clubBattleTeam))
                 {
                     Logger.ForAccount(plr).Information(
                         "Club battle join blocked || Room:{roomId}, PlayerClub:{playerClub}, ExistingClubs:{clubs}",
                         Id,
                         plr.Club?.Id ?? 0,
                          string.Join(",", Players.Values.Where(x => x?.Club?.Id > 0).Select(x => x?.Club?.Id ?? 0).Distinct()));
                     throw new RoomLimitReachedException();
                 }
                 plr.RoomInfo.IsConnecting = false;
                 var joinAsSpectator = false;
                 if (TeamManager.NoSpectatorPlayers.Count() >= Options.PlayerLimit)
                 {
                     if (TeamManager.Spectators.Count() >= Options.SpectatorLimit)
                         throw new RoomLimitReachedException();
                     joinAsSpectator = true;
                 }
                 if (_kickedPlayers.ContainsKey(plr.Account.Id) && plr.Account.SecurityLevel <= SecurityLevel.Tester)
                     throw new RoomAccessDeniedException();
                 if (!_players.ContainsKey(plr.Account.Id))
                 {
                     plr.Channel?.Broadcast(new ChannelLeavePlayerAckMessage(plr.Account.Id));
                     using (_playerSync.Lock())
                     {
                         byte id = 3;
                         while (Players.Values.Any(p => p.RoomInfo.Slot == id))
                             id++;
                         plr.RoomInfo.PeerId = new LongPeerId(plr.Account.Id, new PeerId(0, id, PlayerCategory.Player));
                         plr.RoomInfo.Slot = id;
                     }
                     plr.RoomInfo.Reset();
                     plr.RoomInfo.State = PlayerState.Lobby;
                     plr.RoomInfo.Mode = joinAsSpectator ? PlayerGameMode.Spectate : PlayerGameMode.Normal;
                     plr.RoomInfo.Stats = GameRuleManager.GameRule.GetPlayerRecord(plr);
                     plr.Room = this;
                     plr.RoomInfo.IsConnecting = true;
                     plr.RoomInfo.LastMapID = (byte)Options.MapId;
                _players.TryAdd(plr.Account.Id, plr);
                     TeamManager.Join(plr);
                     ApplyClubBattleTeam(plr, clubBattleTeam);
                     OnPlayerJoining(new RoomPlayerEventArgs(plr));
                     if (GameRuleManager.GameRule != null)
                         plr.stats.OnJoin(GameRuleManager.GameRule);
                     var enterinfo = new RoomEnterRoomInfoAck2Message
                     {
                         RoomId = Id,
                         GameRule = Options.GameRule,
                         MapId = (byte)Options.MapId,
                         PlayerLimit = GetWirePlayerLimit(),
                         GameState = GameState,
                         GameTimeState = SubGameState,
                         TimeLimit = (uint)Options.TimeLimit.TotalMilliseconds,
                         TimeSync = (uint)GameRuleManager.GameRule.RoundTime.TotalMilliseconds,
                         ScoreLimit = Options.ScoreLimit,
                         RelayEndPoint =
                             new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                         LastMapId = plr.RoomInfo.LastMapID,
                     };
                     if (GetRoomPlrDto(plr, true) != null)
                         BroadcastExcept(plr, new RoomEnterPlayerInfoAckMessage(GetRoomPlrDto(plr, true)));
                      if (Options.GameRule != GameRule.Horde)
                      {
                          plr.SendAsync(enterinfo);
                      }
                      else
                      {
                          plr.SendAsync(enterinfo.Map<RoomEnterRoomInfoAck2Message, RoomEnterRoomInfoAckMessage>());
                      }
                    plr.SendAsync(new RoomCurrentCharacterSlotAckMessage(0, plr.RoomInfo.Slot));
                     plr.SendAsync(new RoomPlayerInfoListForEnterPlayerAckMessage(Players.Values.Select(r => GetRoomPlrDto(r)).ToArray()));
                    BroadcastExcept(plr, CreateRoomEnterPlayerAck(plr));
                    foreach (var roomPlr in Players.Values)
                        plr.SendAsync(CreateRoomEnterPlayerAck(roomPlr));
                     RefreshClubInfoSnapshot();
                     plr.SendAsync(new ItemClearInvalidEquipItemAckMessage());
                     plr.SendAsync(new ItemClearEsperChipAckMessage());
                     if (plr.Club != null && plr.Club?.Id > 0)
                     {
                         plr.SendAsync(new ClubClubInfoAckMessage(plr.Map<Player, ClubInfoDto>()));
                         plr.SendAsync(new ClubClubInfoAck2Message(plr.Map<Player, ClubInfoDto2>()));
                     }
                      Broadcast(new ChatPlayerInfoAckMessage(plr.Map<Player, PlayerInfoDto>()));
                      Club.SendAllLivePresenceTo(plr, "ROOM.JOIN.SNAPSHOT");
                      Club.BroadcastLivePresence(plr, "ROOM.JOIN.BROADCAST");
                if (plr.Room.Options.GameRule == GameRule.Arcade)
                    plr.Session.SendAsync(new ArcadeStageSelectAckMessage { Unk1 = 1, Unk2 = 1 });
                var joinedRoom = this;
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(10000);
                        Logger.Information("Stuck-join sweep: account={acc} stillHere={same} handshakePending={c}",
                            plr?.Account?.Id, plr?.Room == joinedRoom, plr?.RoomInfo?.IsConnecting);
                        if (plr?.Room == joinedRoom && plr.RoomInfo?.IsConnecting == true)
                        {
                            Logger.Warning("Stuck-join sweep: evicting account={acc}, handshake never completed", plr?.Account?.Id);
                            joinedRoom.Leave(plr, RoomLeaveReason.AFK);
                            await plr.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Stuck-join sweep threw before it could evict the slot");
                    }
                });
                 }
                 else
                 {
                     plr.Session.SendAsync(new ServerResultAckMessage(ServerResult.DBError));
                 }
         }
        private Team? ResolveClubBattleTeam(Player plr)
        {
            var plrClubId = plr?.Club?.Id ?? 0;
            if (plrClubId <= 0 || TeamManager == null || Players == null)
                return null;
            if (!IsClubBattleRoom())
                return null;
            if (!TeamManager.ContainsKey(Team.Alpha) || !TeamManager.ContainsKey(Team.Beta))
                return null;
            var clubPlayers = Players.Values
                .Where(player => player?.Club?.Id > 0 && player.RoomInfo?.Team != null)
                .ToArray();
            if (clubPlayers.Length == 0)
                return null;
            var sameClub = clubPlayers.FirstOrDefault(player => player?.Club?.Id == plrClubId);
            if (sameClub?.RoomInfo?.Team != null)
                return sameClub.RoomInfo.Team.Team;
            var enemyTeam = clubPlayers[0].RoomInfo.Team.Team;
            return enemyTeam == Team.Alpha ? Team.Beta : Team.Alpha;
        }
        private bool CanJoinClubBattle(Player plr, Team? clubBattleTeam)
        {
            var plrClubId = plr?.Club?.Id ?? 0;
            if (plrClubId <= 0 || Players == null)
                return true;
            if (!IsClubBattleRoom())
                return true;
            var clubIds = Players.Values
                .Where(player => player?.Club?.Id > 0)
                .Select(player => player.Club.Id)
                .Distinct()
                .ToArray();
            if (clubIds.Length >= 2 && !clubIds.Contains(plrClubId))
                return false;
            if (clubBattleTeam == null || !TeamManager.ContainsKey(clubBattleTeam.Value))
                return true;
            var targetTeam = TeamManager[clubBattleTeam.Value];
            return targetTeam.NoSpectatorPlayers.Count() < targetTeam.PlayerLimit;
        }
        private bool IsClubBattleRoom()
        {
            var channelName = RoomManager?.Channel?.Name;
            return channelName?.IndexOf("Clan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   channelName?.IndexOf("Club", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private void ApplyClubBattleTeam(Player plr, Team? clubBattleTeam)
        {
            if (plr?.RoomInfo?.Team == null || clubBattleTeam == null)
                return;
            if (plr.RoomInfo.Team.Team == clubBattleTeam.Value)
                return;
            if (!TeamManager.ContainsKey(clubBattleTeam.Value))
                return;
            var targetTeam = TeamManager[clubBattleTeam.Value];
            if (targetTeam.NoSpectatorPlayers.Count() >= targetTeam.PlayerLimit)
                return;
            TeamManager.ChangeTeam(plr, clubBattleTeam.Value, true);
         }
        private static RoomEnterPlayerAckMessage CreateRoomEnterPlayerAck(Player plr)
        {
            return new RoomEnterPlayerAckMessage(
                plr.Account.Id,
                plr.Account.Nickname,
                1,
                plr.RoomInfo.Mode,
                (int)(plr.Club?.Id ?? 0))
            {
                Team = plr.RoomInfo.Team.Team,
            };
        }
        public RoomPlayerDto GetRoomPlrDto(Player plr, bool newPlr = false)
        {
            if (plr == null)
                return null;
            var dto = new RoomPlayerDto
            {
                ClanId = plr.Club?.Id ?? 0,
                AccountId = plr.Account?.Id ?? 0,
                Nickname = plr.Account?.Nickname ?? "n/A",
                IsGM = plr.Account?.SecurityLevel > SecurityLevel.Tester
            };
            if (newPlr)
            {
                dto.Unk1 = 144;
                dto.Pos = (byte)(plr.Room?.Players.Values.ToList().IndexOf(plr) ?? 0);
            }
            else
            {
#if LATESTS4
                dto.Unk1 = 154;
                dto.Pos = (byte)(plr.Room?.Players.Values.ToList().IndexOf(plr) ?? 0);
#endif
            }
            return dto;
        }
        public void Leave(Player plr, RoomLeaveReason roomLeaveReason = RoomLeaveReason.Left)
        {
            try
            {
                if (plr == null || plr.Room == null || plr.Room != this || !_players.ContainsKey(plr.Account?.Id ?? 0))
                    return;
                GameRuleManager?.GameRule?.OnPlayerLeaving(plr);
                if (roomLeaveReason == RoomLeaveReason.Kicked ||
               roomLeaveReason == RoomLeaveReason.ModeratorKick ||
               roomLeaveReason == RoomLeaveReason.VoteKick)
                    _kickedPlayers.TryAdd(plr.Account.Id, null);
                Broadcast(new RoomLeavePlayerAckMessage(plr.Account.Id, plr.Account.Nickname, roomLeaveReason));
                _players.Remove(plr.Account.Id, out _);
                TeamManager.Leave(plr);
                RefreshClubInfoSnapshot();
                RefreshClanRoomName();
                plr.RoomInfo.PeerId = 0;
                plr.Room = null;
                plr.RoomInfo.IsReady = false;
                Network.Services.IpcService.NotifyPlayerLeftRoom(plr.Account.Id);
                plr.SendAsync(new RoomLeavePlayerInfoAckMessage(plr.Account.Id));
                plr.SendAsync(new ItemClearInvalidEquipItemAckMessage());
                plr.SendAsync(new ItemClearEsperChipAckMessage());
                plr.Channel?.BroadcastExcept(plr, new ChannelEnterPlayerAckMessage(plr.Map<Player, PlayerInfoShortDto>()));
                plr.Channel?.NewPlayerList(plr, 1);
                plr.Channel?.SendPlayerlist(plr);
                Club.SendAllLivePresenceTo(plr, "ROOM.LEAVE.SNAPSHOT");
                Club.BroadcastLivePresence(plr, "ROOM.LEAVE.BROADCAST");
                if (TeamManager.Players.Any())
                {
                    ChangeMasterIfNeeded(GetPlayerWithLowestPing());
                    ChangeHostIfNeeded(GetPlayerWithLowestPing());
                    OnPlayerLeft(new RoomPlayerEventArgs(plr));
                }
                else
                {
                    RoomManager?.Remove(this);
                }
            }
            catch { }
        }
        public void RefreshClubInfoSnapshot()
        {
            var orderedPlayers = new List<Player>();
            void AddTeamPlayers(Team team)
            {
                if (!TeamManager.ContainsKey(team))
                    return;
                orderedPlayers.AddRange(TeamManager[team].NoSpectatorPlayers
                    .Where(player => player?.Club?.Id > 0));
            }
            AddTeamPlayers(Team.Alpha);
            AddTeamPlayers(Team.Beta);
            orderedPlayers.AddRange(Players.Values
                .Where(player => player?.Club?.Id > 0 &&
                                 orderedPlayers.All(existing => existing.Account.Id != player.Account.Id)));
            var clubPlayers = orderedPlayers
                .GroupBy(player => player?.Club?.Id ?? 0)
                .Where(group => group.Key > 0)
                .Select(group => group.First())
                .ToArray();
            var clubs = clubPlayers
                .Select(player => player.Map<Player, PlayerClubInfoDto>())
                .ToArray();
            Broadcast(new RoomClubInfoListForEnterPlayerAckMessage(clubs));
            foreach (var club in clubs)
                Broadcast(new RoomEnterClubInfoAckMessage(club));
        }
        public static bool CustomRules(Player plr, bool silent)
        {
            if (plr.Room.Options.GameRule == GameRule.Arcade)
                return true;
            if (!CustomRuleRooms.CustomRules(plr))
            {
                plr.RoomInfo.IsReady = false;
                plr.Room.Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
                plr.SendAsync(new ServerResultAckMessage(ServerResult.WearingUnusableItem));
                return false;
            }
            return true;
        }
        public void BeginRound(Player plr)
        {
            if (Disposed || GameState != GameState.Waiting || plr.RoomInfo.State != PlayerState.Lobby ||
                plr != Master)
                return;
            if (CustomRules(plr, false) == false)
                return;
            var stateMachine = plr.Room.GameRuleManager.GameRule.StateMachine;
            if (stateMachine.CanFire(GameRuleStateTrigger.StartPrepare))
            {
                stateMachine.Fire(GameRuleStateTrigger.StartPrepare);
                return;
            }
            plr.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
        }
        public void ChangeReadyStatus(Player plr)
        {
            if (Disposed || plr.Room != this || plr == Master)
                return;
            if (CustomRules(plr, false) == false)
                return;
            if (IsChangingRules)
            {
                plr.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                plr.RoomInfo.IsReady = false;
                Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
                return;
            }
            if (HasStarted)
                plr.SendAsync(new RoomGameLoadingAckMessage());
            if (GameState != GameState.Waiting)
            {
                plr.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
                return;
            }
            else
            {
                plr.RoomInfo.IsReady = !plr.RoomInfo.IsReady;
                Broadcast(new RoomReadyRoundAckMessage(plr.Account.Id, plr.RoomInfo.IsReady));
            }
        }
        public void IntrudeRoom(Player plr)
        {
            if (Disposed || plr.Room != this || GameState == GameState.Waiting)
                return;
            if (GameState == GameState.Result || GameRuleState == GameRuleState.EnteringResult)
            {
                plr.SendAsync(new GameEventMessageAckMessage(GameEventMessage.RoomModeIsChanging, 0, 0, 0, ""));
                return;
            }
            if (IsPreparing || !HasStarted)
            {
                plr.SendAsync(new GameEventMessageAckMessage(GameEventMessage.CantStartGame, 0, 0, 0, ""));
                return;
            }
            plr.SendAsync(new RoomGameLoadingAckMessage());
        }
        private void RefreshClanRoomName()
        {
            if (Disposed || Options == null || RoomManager?.Channel == null)
                return;
            var clanName = Master?.Club?.ClanName;
            if (string.IsNullOrWhiteSpace(clanName))
            {
                clanName = Players.Values
                    .Where(player => player?.Club?.Id > 0)
                    .Select(player => player.Club.ClanName)
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));
            }
            if (string.IsNullOrWhiteSpace(clanName) ||
                string.Equals(Options.Name, clanName, StringComparison.Ordinal))
                return;
            Options.Name = clanName;
            RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
        }
        public void SetCreator(Player plr)
        {
            Master = plr;
            Host = plr;
        }
        public bool ChangeMasterIfNeeded(Player plr, bool force = false)
        {
            if (Disposed)
                return false;
            if (plr == null)
                return false;
            if (!plr.Room.Players.Any())
                return false;
            if (Master == null)
                force = true;
            if (plr == Master || (Master?.IsLoggedIn() ?? false) && !force || !plr.IsLoggedIn())
                return false;
            Master = plr;
            if (Master.RoomInfo.IsReady)
                Master.RoomInfo.IsReady = false;
            RefreshClanRoomName();
            Broadcast(new RoomChangeMasterAckMessage(Master.Account.Id));
            return true;
        }
        public bool ChangeHostIfNeeded(Player plr, bool force = false)
        {
            if (Disposed)
                return false;
            if (plr == null)
                return false;
            if (!plr.Room.Players.Any())
                return false;
            if (Host == null)
                force = true;
            if (Host == plr || (Host?.IsLoggedIn() ?? false) && !force || !plr.IsLoggedIn())
                return false;
            Logger.ForAccount(plr).Information("Room {roomId}: relay duty reassigned, latency {ping} ms, forced {f}", Id,
                plr.Session.UnreliablePing, force.ToString());
            Host = plr;
            Broadcast(new RoomChangeRefereeAckMessage(Host.Account.Id));
            return true;
        }
        public void ChangeRules(ChangeRuleDto options)
        {
            ChangeRules2(options.Map<ChangeRuleDto, ChangeRuleDto2>());
        }
        public void ChangeRules2(ChangeRuleDto2 options)
        {
            if (Disposed)
                return;
            if (IsChangingRules)
            {
                Master?.SendAsync(new ServerResultAckMessage(ServerResult.RoomChangingRules));
                return;
            }
            var rawClientLimit = options.PlayerLimit;
            var isArcade = IsArcadeRoom(Options.GameRule, options.GameRule);
            if (isArcade)
            {
                options.GameRule = GameRule.Arcade;
                options.PlayerLimit = NormalizeArcadePlayerLimitFromClient(options.PlayerLimit);
                options.SpectatorLimit = 0;
                if (options.MapId == 0)
                    options.MapId = (byte)Options.MapId;
            }
            var activePlayers = TeamManager.NoSpectatorPlayers.Count();
            var spectators = TeamManager.Spectators.Count();
            var alphaPlayers = TeamManager.ContainsKey(Team.Alpha) ? TeamManager[Team.Alpha].NoSpectatorPlayers.Count() : 0;
            var alphaLimit = TeamManager.ContainsKey(Team.Alpha) ? TeamManager[Team.Alpha].PlayerLimit : 0;
            var betaPlayers = TeamManager.ContainsKey(Team.Beta) ? TeamManager[Team.Beta].NoSpectatorPlayers.Count() : 0;
            var betaLimit = TeamManager.ContainsKey(Team.Beta) ? TeamManager[Team.Beta].PlayerLimit : 0;
            Logger.ForAccount(Master).Information(
                "Settings update arriving for room={roomId}: arcadePath={arcade} limitAsSent={rawLimit} limitApplied={reqLimit} inPlay={active} watching={spectators} tracked={players}",
                Id, isArcade, rawClientLimit, options.PlayerLimit, activePlayers, spectators, Players.Count);
            if (!isArcade && options.PlayerLimit < activePlayers)
            {
                Logger.ForAccount(Master).Error(
                    "Refusing the smaller capacity: {active} people are already playing but only {req} were requested",
                    activePlayers, options.PlayerLimit);
                Master?.SendAsync(new ServerResultAckMessage(ServerResult.PlayerLimitReached));
                return;
            }
            if (!isArcade && options.SpectatorLimit < (byte)spectators)
            {
                Logger.ForAccount(Master).Error(
                    "Refusing the smaller viewer capacity: {spectators} are already watching but only {req} were requested",
                    spectators, options.SpectatorLimit);
                Master?.SendAsync(new ServerResultAckMessage(ServerResult.PlayerLimitReached));
                return;
            }
            if (!RoomManager.GameRuleFactory.Contains(options.GameRule))
            {
                Logger.ForAccount(Master).Error("No implementation registered for requested mode {0}", options.GameRule);
                Master?.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (string.IsNullOrWhiteSpace(options.Name) || string.IsNullOrEmpty(options.Name))
            {
                Master?.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            var israndom = false;
            var maps = GameServer.Instance.ResourceCache.GetMaps();
            var map = maps.FirstOrDefault(x => x.Value.byteId == options.MapId && x.Value.GameRule == options.GameRule)
                        .Value;
            if (options.GameRule == GameRule.Random)
            {
                var randomgr = new SecureRandom().Next(1, 5);
                israndom = true;
                switch (randomgr)
                {
                    case 1:
                        options.GameRule = GameRule.Touchdown;
                        break;
                    case 2:
                        options.GameRule = GameRule.Chaser;
                        break;
                    case 3:
                        options.GameRule = GameRule.Deathmatch;
                        break;
                    case 4:
                        options.GameRule = GameRule.BattleRoyal;
                        break;
                    case 5:
                        options.GameRule = GameRule.Captain;
                        break;
                    case 6:
                        options.GameRule = GameRule.Siege;
                        break;
                }
                var modeMaps = maps.Where(x => x.Value.GameRule == options.GameRule && !x.Value.IsRandom);
                var selmap = modeMaps.ElementAtOrDefault(new SecureRandom().Next(0, modeMaps.Count()));
                options.MapId = (byte)selmap.Key;
            }
            if (!Master.Channel?.RoomManager.GameRuleFactory.Contains(options.GameRule) ?? false)
            {
                Logger.ForAccount(Master)
                    .Error("This channel has no implementation registered for mode {gameRule}", options.GameRule);
                Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (map == null && isArcade)
                map = maps.GetValueOrDefault(Options.MapId);
            if (map == null)
            {
                Logger.ForAccount(Master)
                    .Error("Stage id {map} is absent from the loaded map table", options.MapId);
                Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (map.IsRandom && map.GameRule == options.GameRule)
            {
                israndom = true;
                var modeMaps = maps.Where(x => x.Value.GameRule == options.GameRule && !x.Value.IsRandom);
                var selmap = modeMaps.ElementAtOrDefault(new SecureRandom().Next(0, modeMaps.Count()));
                options.MapId = (byte)selmap.Key;
            }
            map = maps.GetValueOrDefault(options.MapId);
            Logger.ForAccount(Master).Information("Applying new setup to room {id}: mode {mode} on stage {mapid}", Id, options.GameRule,
                options.MapId);
            if (options.GameRule != GameRule.Practice &&
                options.GameRule != GameRule.CombatTrainingTD &&
                options.GameRule != GameRule.CombatTrainingDM)
            {
                if (map.GameRule != options.GameRule)
                {
                    Logger.ForAccount(Master).Error("Map {mapId}({mapName}) is not available for game rule {gameRule}",
                        map.Id, map.Name, options.GameRule);
                    Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (options.GameRule == GameRule.Practice)
                {
                    if (!Namecheck.IsNameValid(options.Name, true))
                    {
                        Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                        return;
                    }
                }
                if (options.GameRule == GameRule.Practice)
                {
                    if (!Namecheck.IsNameValid(options.Name, true))
                    {
                        Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                        return;
                    }
                }
            }
            if (!isArcade && options.PlayerLimit > map.MaxPlayers)
            {
                Logger.ForAccount(Master).Error("Wrong playerLimit for Map {0}", map.Id);
                Master.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                return;
            }
            var isfriendly = false;
            var isburning = false;
            var isWithoutStats = false;
            switch (options.FMBurnMode)
            {
                case 0:
                    isfriendly = false;
                    break;
                case 1:
                    isfriendly = true;
                    break;
                case 2:
                    isfriendly = false;
                    isburning = true;
                    break;
                case 3:
                    isburning = true;
                    isfriendly = true;
                    break;
                case 4:
                    isWithoutStats = true;
                    break;
                case 5:
                    isWithoutStats = isfriendly = true;
                    break;
            }
            _changingRulesTimer = TimeSpan.Zero;
            IsChangingRules = true;
            Options.ChangeRuleId = options.ChangeRuleId;
            Options.Name = options.Name;
            Options.MapId = options.MapId;
            Options.PlayerLimit = options.PlayerLimit;
            Options.GameRule = options.GameRule;
            Options.TimeLimit = TimeSpan.FromMinutes(options.Time);
            Options.ScoreLimit = options.Points;
            Options.Unk1 = options.Unk1;
            Options.Password = options.Password;
            Options.IsFriendly = isfriendly;
            Options.IsBurning = isburning;
            Options.IsRandom = israndom;
            Options.ItemLimit = (byte)options.ItemLimit;
            Options.HasSpectator = options.HasSpectator;
            Options.Unk3 = options.Unk3;
            Options.SpectatorLimit = options.SpectatorLimit;
            Options.IsWithoutStats = isWithoutStats;
            Players.Values.ToList().ForEach(playr => { playr.RoomInfo.IsReady = false; });
            foreach (var plr in Players.Values)
            {
                _roomChangePlayers.Add(plr, plr.RoomInfo.Mode);
                if (TeamManager.ContainsKey(Team.Alpha) && plr.RoomInfo.Team.Team == Team.Alpha)
                    _roomChangeAlphaPlayers.Add(plr, plr.RoomInfo.Mode);
                if (TeamManager.ContainsKey(Team.Beta) && plr.RoomInfo.Team.Team == Team.Beta)
                    _roomChangeBetaPlayers.Add(plr, plr.RoomInfo.Mode);
                plr.stats.OnJoin(RoomManager.GameRuleFactory.Get(Options.GameRule, this));
                plr.RoomInfo.IsReady = false;
            }
            // asignar GameRuleManager.GameRule dispara el briefing, y cada modo lo serializa con
            // un largo distinto. Si llega primero, el cliente lo lee con el layout viejo y crashea.
            var changeRuleAck = Options.Map<RoomCreationOptions, ChangeRuleDto2>();
            if (Options.GameRule == GameRule.Arcade)
                changeRuleAck.PlayerLimit = ArcadePlayerLimitToClient(Options.PlayerLimit);
            Broadcast(new RoomChangeRuleNotifyAck2Message(changeRuleAck));

            GameRuleManager.MapInfo = GameServer.Instance.ResourceCache.GetMaps()[Options.MapId];
            GameRuleManager.GameRule = RoomManager.GameRuleFactory.Get(Options.GameRule, this);
        }
        private Player GetPlayerWithLowestPing()
        {
            return TeamManager.Players.OrderBy(x => x.Session?.UnreliablePing ?? double.MaxValue).FirstOrDefault() ?? null;
        }
        private void TeamManager_TeamChanged(object sender, TeamChangedEventArgs e)
        {
        }
        private void GameRuleManager_OnGameRuleChanged(object sender, EventArgs e)
        {
            if (Disposed)
                return;
            GameRuleManager.GameRule.StateMachine.OnTransitioned(t => OnStateChanged());
            try
            {
                // Primero el record del modo nuevo: si se rearman los equipos con el record viejo
                // todavia puesto, cualquier briefing que salga en el medio se serializa con el
                // layout del modo anterior y el cliente lo lee mal.
                foreach (var plr in Players.Values)
                    plr.RoomInfo.Stats = GameRuleManager.GameRule.GetPlayerRecord(plr);

                if (TeamManager.ContainsKey(Team.Alpha))
                {
                    foreach (var plrI in _roomChangeAlphaPlayers)
                    {
                        var plr = plrI.Key;
                        TeamManager.JoinDirectly(plr, Team.Alpha);
                        TeamManager.ChangeMode(plr, plrI.Value);
                    }
                }
                if (TeamManager.ContainsKey(Team.Beta))
                {
                    foreach (var plrI in _roomChangeBetaPlayers)
                    {
                        var plr = plrI.Key;
                        TeamManager.JoinDirectly(plr, Team.Beta);
                        TeamManager.ChangeMode(plr, plrI.Value);
                    }
                }
                foreach (var plr in Players.Values)
                {
                    plr.RoomInfo.Stats = GameRuleManager.GameRule.GetPlayerRecord(plr);
                }
            }
            catch (Exception exception)
            {
                Logger.Error(exception.ToString());
            }
            finally
            {
                _roomChangePlayers.Clear();
                _roomChangeAlphaPlayers.Clear();
                _roomChangeBetaPlayers.Clear();
                BroadcastBriefing();
            }
        }
        #region Events
        public event EventHandler<RoomPlayerEventArgs> PlayerJoining;
        public event EventHandler<RoomPlayerEventArgs> PlayerJoined;
        public event EventHandler<RoomPlayerEventArgs> PlayerLeft;
        public event EventHandler StateChanged;
        internal virtual byte GetFMBurnModeInfo()
        {
            byte fmBurnMode = 0;
            if (Options.IsFriendly && Options.IsWithoutStats)
                fmBurnMode = 5;
            else if (Options.IsWithoutStats)
                fmBurnMode = 4;
            else if (Options.IsFriendly && Options.IsBurning)
                fmBurnMode = 3;
            else if (Options.IsBurning)
                fmBurnMode = 2;
            else if (Options.IsFriendly)
                fmBurnMode = 1;
            else if (!Options.IsFriendly && !Options.IsBurning)
                fmBurnMode = 0;
            return fmBurnMode;
        }
        internal virtual RoomDto GetRoomInfo()
        {
            var roomDto = new RoomDto
            {
                RoomId = (byte)Id,
                PlayerCount = (byte)Players.Count,
                PlayerLimit = GetWirePlayerLimit(),
                State = (byte)GameRuleManager.GameRule.StateMachine.State,
                GameRule = (int)Options.GameRule,
                Map = (byte)Options.MapId,
                WeaponLimit = Options.ItemLimit,
                Name = Options.Name,
                Password = Options.Password,
                FMBURNMode = GetFMBurnModeInfo(),
                HasSpectator = Options.HasSpectator,
                IsRandom = Options.IsRandom ? 1 : 0,
                CreationId = Options.UniqueId
            };
            return roomDto;
        }
        internal virtual void OnPlayerJoining(RoomPlayerEventArgs e)
        {
            if (Disposed || e.Player == null)
                return;
            PlayerJoining?.Invoke(this, e);
            RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
        }
        internal virtual void OnPlayerJoined(RoomPlayerEventArgs e)
        {
            if (Disposed || e.Player == null)
                return;
            PlayerJoined?.Invoke(this, e);
            RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
        }
        protected virtual void OnPlayerLeft(RoomPlayerEventArgs e)
        {
            if (Disposed || e.Player == null)
                return;
            PlayerLeft?.Invoke(this, e);
            RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
        }
        protected virtual void OnStateChanged()
        {
            if (Disposed)
                return;
            StateChanged?.Invoke(this, EventArgs.Empty);
            RoomManager.Channel.BroadcastCencored(new RoomChangeRoomInfoAck2Message(GetRoomInfo()));
        }
        #endregion
        #region Broadcast
        public void BroadcastNotice(string message)
        {
            Broadcast(new NoticeAdminMessageAckMessage(message));
        }
        public void Broadcast(object message)
        {
            if (message == null)
                return;
            var players = ProudNetSrc.Serialization.Packet.ChannelOf(message) == ProudNetSrc.Serialization.PacketType.Chat
                ? (IEnumerable<Player>)TeamManager.Players
                : Players.Values;
            foreach (var plr in players)
                plr.SendAsync(message);
        }
        public void BroadcastExcept(Player blacklisted, object message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(x => x != blacklisted))
                plr.SendAsync(message);
        }
        public void BroadcastExcept(List<Player> blacklist, object message)
        {
            if (message == null)
                return;
            foreach (var plr in Players.Values.Where(x => !blacklist.Contains(x)))
                plr.SendAsync(message);
        }
        public void SendBriefing(Player plr, bool isResult = false)
        {
            if (plr == null || plr.Room == null)
                return;
            var data = GameRuleManager.GameRule.Briefing.SerializeDataToArray(isResult);
            plr.SendAsync(new GameBriefingInfoAckMessage(isResult, false, data));
        }
        public void BroadcastBriefing(bool isResult = false)
        {
            if (IsChangingRules)
                return;

            foreach (var plr in Players.Values.ToList())
                SendBriefing(plr, isResult);
        }
        #endregion
    }
}

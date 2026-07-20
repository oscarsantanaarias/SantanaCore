using System.Threading.Tasks;
namespace Santana.Network.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using ExpressMapper.Extensions;
    using Santana.Game.GameRules;
    using Santana.Network.Data.Game;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using ProudNetSrc.Handlers;
    using Serilog;
    using Serilog.Core;
    using Santana.Network.Message.Chat;
    using Santana.Database.Game;
    using Santana.Database.Auth;
    using Dapper.FastCrud;
    using Santana.Game;
    using MySqlConnector;
    internal class RoomService : ProudMessageHandler
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(RoomService));
        [MessageHandler(typeof(PlayerBadUserReqMessage))]
        public void PlayerBadUserReq(GameSession session, PlayerBadUserReqMessage message)
        {
            _log.Information("Anticheat report raised by {nick}, payload {unk}",
                session.Player?.Account.Nickname, message.Unk);
        }
        [MessageHandler(typeof(RoomAutoMixingTeamReqMessage))]
        public void RoomAutoMixingTeamReq(GameSession session, RoomAutoMixingTeamReqMessage message)
        {
            var gamer = session.Player;
            var gameRoom = gamer.Room;
            if (gameRoom == null)
                return;
            if (gamer != gamer.Room.Master || gamer.Room.GameState != GameState.Waiting)
                return;
            foreach (var occupant in gameRoom.Players.Values.ShuffleSecure())
            {
                var pickedTeam = gameRoom.TeamManager.Keys.ShuffleSecure().FirstOrDefault();
                gameRoom.TeamManager.ChangeTeam(occupant, pickedTeam, true);
            }
        }
        [MessageHandler(typeof(RoomInfoRequestReqMessage))]
        public void RoomInfoRequestReq(GameSession session, RoomInfoRequestReqMessage message)
        {
            var gamer = session.Player;
            var targetRoom = gamer.Channel?.RoomManager[message.RoomId] ?? null;
            if (targetRoom == null)
                return;
            session.SendAsync(new RoomInfoRequestAck2Message
            {
                Info = new RoomInfoRequestDto
                {
                    MasterName = targetRoom.Master.Account.Nickname,
                    MasterLevel = targetRoom.Master.Level,
                    ScoreLimit = targetRoom.Options.ScoreLimit,
                    TimeLimit = targetRoom.Options.TimeLimit,
                    State = targetRoom.GameState,
                    IsMasterInClan = false,
                    Unk8 = 1,
                    Unk9 = 1
                }
            });
        }
        [MessageHandler(typeof(RoomEnterPlayerReqMessage))]
        public void CEnterPlayerReq(GameSession session)
        {
            var gamer = session.Player;
            if (gamer == null)
                return;
            if (gamer.Room == null)
            {
                _log.Information("{0} sent a room request while outside any room", gamer.Account.Nickname);
                session.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                return;
            }
            var pingCap = gamer.Room.Options.Ping;
            if (pingCap > 0 && pingCap < 1000 && gamer.Session.UnreliablePing > pingCap)
            {
                if (gamer.Room != null)
                    gamer.Room.Leave(gamer, RoomLeaveReason.Left);
                return;
            }
            if (!string.IsNullOrEmpty(gamer.Room.Options.Region))
            {
                if (gamer.Room.Options.Region != new WebClient().DownloadString($"https://ipapi.co/{session.RemoteEndPoint.Address}/continent_code"))
                {
                    return;
                }
            }
            gamer.RoomInfo.IsConnecting = false;
            if (!gamer.Room.ChangeMasterIfNeeded(gamer))
                gamer.SendAsync(new RoomChangeMasterAckMessage(gamer.Room.Master.Account.Id));
            if (!gamer.Room.ChangeHostIfNeeded(gamer))
                gamer.SendAsync(new RoomChangeRefereeAckMessage(gamer.Room.Host.Account.Id));
            gamer.Room.Broadcast(new RoomEnterPlayerForBookNameTagsAckMessage
            {
                AccountId = gamer.Account.Id,
                Team = gamer.RoomInfo.Team.Team,
                PlayerGameMode = gamer.RoomInfo.Mode,
                Exp = gamer.TotalExperience,
                Nickname = gamer.Account.Nickname,
                Unk1 = gamer.NameTag,
                Unk2 = (byte)(gamer.NameTag > 0 ? 1 : 0)
            });
            AuthService.LoadPlayerNameTag(gamer, true, false);
            gamer.Room.SendBriefing(gamer);
            gamer.Room.GameRuleManager.GameRule.RoomJoinCompleted(gamer);
        }
        [MessageHandler(typeof(RoomMakeReqMessage))]
        public async Task CMakeRoomReq(GameSession session, RoomMakeReqMessage message)
        {
            await CMakeRoomReq2(session, message.Map<RoomMakeReqMessage, RoomMakeReq2Message>());
        }
        [MessageHandler(typeof(RoomMakeReq2Message))]
        public async Task CMakeRoomReq2(GameSession session, RoomMakeReq2Message message)
        {
            try
            {
                var gamer = session.Player;
                if (gamer?.Room != null)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                    return;
                }
                if (gamer?.Channel == null)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.JoinChannelFailed));
                    return;
                }
                if (gamer?.Channel?.Id < 1)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if ((gamer.Channel?.Name?.Contains("Clan") ?? false) && (gamer.Club?.Id ?? 0) <= 0)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                var isRandomRoom = false;
                var mapCatalog = GameServer.Instance.ResourceCache.GetMaps();
                var chosenMap = mapCatalog.Values.Where(x => x.byteId == message.MapId && x.GameRule == message.GameRule).FirstOrDefault();
                if (!gamer.Channel?.RoomManager.GameRuleFactory.Contains(message.GameRule) ?? false)
                {
                    _log.ForAccount(gamer)
                        .Error("Create room denied: channel has no factory for game rule {0}", message.GameRule);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (gamer.Channel.Id == 5 && (message.GameRule != GameRule.Touchdown && message.GameRule != GameRule.Deathmatch && message.GameRule != GameRule.PassTouchdown))
                {
                    _log.ForAccount(gamer)
                        .Error("Create room denied: ranked channel does not accept game rule {0}", message.GameRule);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (gamer.Channel.Id == 5 && message.PlayerLimit < 6)
                {
                    _log.ForAccount(gamer)
                        .Error("Create room denied: ranked channel needs at least 6 slots, got {0}", message.PlayerLimit);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (gamer.Channel.Id == 5 &&  !string.IsNullOrEmpty(message.Password))
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game room with a password");
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (gamer.Channel.Id == 5 && (message.TimeLimit > 30 || message.TimeLimit < 20))
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game bad time");
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (gamer.Channel.Id == 5 && (message.FMBURNMode > 0))
                {
                    _log.ForAccount(gamer)
                        .Error("Rank FMBurnMode");
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (chosenMap == null)
                {
                    _log.ForAccount(gamer)
                        .Error("Map {map} does not exist", message.MapId);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                if (message.GameRule == GameRule.Random)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                var wasRandomMode = false;
                if (message.GameRule == GameRule.Random)
                {
                    var rolledRule = new SecureRandom().Next(1, 4);
                    wasRandomMode = true;
                    isRandomRoom = true;
                    switch (rolledRule)
                    {
                        case 1:
                            message.GameRule = GameRule.Touchdown;
                            break;
                        case 2:
                            message.GameRule = GameRule.Chaser;
                            break;
                        case 3:
                            message.GameRule = GameRule.Deathmatch;
                            break;
                        case 4:
                            message.GameRule = GameRule.BattleRoyal;
                            break;
                        case 5:
                            message.GameRule = GameRule.Captain;
                            break;
                        case 6:
                            message.GameRule = GameRule.Siege;
                            break;
                    }
                    var ruleMaps = mapCatalog.Where(x => x.Value.GameRule == message.GameRule && !x.Value.IsRandom);
                    var pickedEntry = ruleMaps.ElementAtOrDefault(new SecureRandom().Next(0, ruleMaps.Count()));
                    message.MapId = (byte)pickedEntry.Key;
                }
                if (chosenMap.IsRandom && chosenMap.GameRule == message.GameRule)
                {
                    isRandomRoom = true;
                    var ruleMaps = mapCatalog.Where(x => x.Value.GameRule == message.GameRule && !x.Value.IsRandom);
                    var pickedEntry = ruleMaps.ElementAtOrDefault(new SecureRandom().Next(0, ruleMaps.Count()));
                    message.MapId = (byte)pickedEntry.Key;
                }
                chosenMap = mapCatalog.GetValueOrDefault(message.MapId);
                _log.ForAccount(gamer)
                    .Information("CreateRoom || Room: {mode}, {mapid}", message.GameRule, message.MapId);
                if (message.GameRule != GameRule.Practice &&
                    message.GameRule != GameRule.CombatTrainingTD &&
                    message.GameRule != GameRule.CombatTrainingDM)
                {
                    if (chosenMap?.GameRule != null && chosenMap.GameRule != message.GameRule)
                    {
                        _log.ForAccount(gamer).Error("Map {mapId}({mapName}) is not available for game rule {gameRule}",
                            chosenMap.Id, chosenMap.Name, message.GameRule);
                        await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                        return;
                    }
                    if (message.GameRule == GameRule.Practice)
                    {
                        if (!Namecheck.IsNameValid(message.Name, true))
                        {
                            await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                            return;
                        }
                    }
                }
                if ((message?.PlayerLimit != null || chosenMap?.MaxPlayers != null) && message.PlayerLimit > chosenMap.MaxPlayers)
                {
                    _log.ForAccount(gamer).Error("Wrong playerLimit for Map {0}", chosenMap.Id);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToCreateRoom));
                    return;
                }
                var friendlyFire = false;
                var burningMode = false;
                var statsDisabled = false;
                var blockIntrusion = message.GameRule == GameRule.Horde;
                if (message.GameRule == GameRule.CombatTrainingDM ||
                    message.GameRule == GameRule.CombatTrainingTD ||
                    message.GameRule == GameRule.Practice)
                {
                    friendlyFire = true;
                    blockIntrusion = true;
                    message.PlayerLimit = 1;
                }
                switch (message.FMBURNMode)
                {
                    case 0:
                        friendlyFire = false;
                        break;
                    case 1:
                        friendlyFire = true;
                        break;
                    case 2:
                        friendlyFire = false;
                        burningMode = true;
                        break;
                    case 3:
                        burningMode = true;
                        friendlyFire = true;
                        break;
                    case 4:
                        statsDisabled = true;
                        break;
                    case 5:
                        statsDisabled = friendlyFire = true;
                        break;
                }
                if (message.GameRule == GameRule.Arcade)
                {
                    var requestedLimit = message.PlayerLimit;
                    message.PlayerLimit = Room.NormalizeArcadePlayerLimitFromClient(message.PlayerLimit);
                    _log.ForAccount(gamer).Information(
                        "CreateRoom arcade coop rawLimit={rawLimit} reqLimit={reqLimit}",
                        requestedLimit, message.PlayerLimit);
                }
                var newRoom = gamer.Channel.RoomManager.Create_2(
                    new RoomCreationOptions
                    {
                        Name = message.Name,
                        GameRule = message.GameRule,
                        PlayerLimit = message.PlayerLimit,
                        TimeLimit = TimeSpan.FromMinutes(message.TimeLimit),
                        ScoreLimit = (ushort)message.ScoreLimit,
                        Unk1 = 1,
                        Password = message.Password,
                        IsFriendly = friendlyFire,
                        IsBurning = burningMode,
                          S5Mode = burningMode,
                        IsWithoutStats = statsDisabled,
                        MapId = message.MapId,
                        ItemLimit = (byte)message.WeaponLimit,
                        IsNoIntrusion = blockIntrusion,
                        SpectatorLimit = message.SpectatorLimit,
                        IsRandom = isRandomRoom,
                        HasSpectator = message.SpectatorLimit > 0,
                        Unk3 = 257,
                        UniqueId = message.CreationId,
                        ServerEndPoint =
                            new IPEndPoint(IPAddress.Parse(Config.Instance.IP), Config.Instance.RelayListener.Port),
                        Creator = gamer
                    });
                newRoom.Join(gamer);
                CustomRuleRooms.SetCustomRules(newRoom);
                if (!gamer.Channel.RoomManager._rooms.TryAdd(newRoom.Id, newRoom))
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomAccessDeniedException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterBecauseKicked));
            }
            catch (RoomLimitReachedException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitIsNoIntrutionException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (RoomException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
        }
        [MessageHandler(typeof(RoomChoiceMasterChangeReqMessage))]
        public void RoomChoiceMasterChangeReq(GameSession session, RoomChoiceMasterChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer == null)
                return;
            if (gamer.Room == null)
                return;
            if (gamer.Room.Master != gamer)
                return;
            var newMaster = GameServer.Instance.PlayerManager.FirstOrDefault(candidate =>
                candidate.Room == gamer.Room && candidate.Account.Id == message.AccountId);
            if (newMaster == null)
            {
                gamer.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            gamer.Room.ChangeMasterIfNeeded(newMaster, true);
            gamer.Room.ChangeHostIfNeeded(newMaster, true);
        }
        [MessageHandler(typeof(RoomChoiceTeamChangeReqMessage))]
        public void CMixChangeTeamReq(GameSession session, RoomChoiceTeamChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer != gamer.Room.Master && gamer.Room.GameState != GameState.Waiting)
                return;
            var moving = gamer.Room.Players.GetValueOrDefault(message.PlayerToMove);
            var replaced = gamer.Room.Players.GetValueOrDefault(message.PlayerToReplace);
            var sourceTeam = gamer.Room.TeamManager[message.FromTeam];
            var destTeam = gamer.Room.TeamManager[message.ToTeam];
            var gameRoom = gamer.Room;
            if (sourceTeam == null || destTeam == null || moving == null ||
                sourceTeam != moving.RoomInfo.Team ||
                (replaced != null && destTeam != replaced.RoomInfo.Team))
            {
                session.SendAsync(new RoomMixedTeamBriefingInfoAckMessage());
                return;
            }
            if (replaced == null)
            {
                try
                {
                    gameRoom.TeamManager.Join(moving);
                    gamer.Room.BroadcastBriefing();
                }
                catch (TeamLimitReachedException)
                {
                    session.SendAsync(new RoomChoiceTeamChangeFailAckMessage());
                }
            }
            else
            {
                gameRoom.TeamManager.ChangeTeam(moving, destTeam.Team, false);
                gameRoom.TeamManager.ChangeTeam(replaced, sourceTeam.Team, false);
                gamer.Room.Broadcast(new RoomChoiceTeamChangeAckMessage(moving.Account.Id, replaced.Account.Id,
                    sourceTeam.Team, destTeam.Team));
                gamer.Room.BroadcastBriefing();
            }
        }
        [MessageHandler(typeof(InGamePlayerResponseReqMessage))]
        public void InGamePlayerResponseReq(GameSession session, InGamePlayerResponseReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.RoomInfo == null || gamer.RoomInfo?.State == PlayerState.Lobby)
                return;
            gamer.RoomInfo.State = PlayerState.Alive;
        }
        [MessageHandler(typeof(RoomEnterReqMessage))]
        public async Task CGameRoomEnterReq(GameSession session, RoomEnterReqMessage message)
        {
            try
            {
                var gamer = session.Player;
                if (gamer.Room != null || gamer.Channel == null)
                {
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                    return;
                }
                if (gamer.Channel.RoomManager._rooms.TryGetValue(message.RoomId, out var gameRoom))
                {
                    if ((gameRoom.RoomManager?.Channel?.Name?.Contains("Clan") ?? false) && (gamer.Club?.Id ?? 0) <= 0)
                    {
                        await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
                        return;
                    }
                    if (gameRoom.IsChangingRules)
                    {
                        await session.SendAsync(new ServerResultAckMessage(ServerResult.RoomChangingRules));
                        return;
                    }
                    if (!string.IsNullOrEmpty(gameRoom.Options.Password) &&
                        !gameRoom.Options.Password.Equals(message.Password) &&
                        gamer.Account.SecurityLevel <= SecurityLevel.Tester)
                    {
                        await session.SendAsync(new ServerResultAckMessage(ServerResult.PasswordError));
                        return;
                    }
                    gameRoom.Join(gamer);
                }
                else
                {
                    _log.ForAccount(gamer).Error("Room {roomId} in channel {channelId} not found", message.RoomId,
                        gamer.Channel.Id);
                    await session.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                }
            }
            catch (RoomAccessDeniedException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterBecauseKicked));
            }
            catch (RoomLimitReachedException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.CantEnterRoom));
            }
            catch (RoomLimitIsNoIntrutionException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (RoomException)
            {
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                await session.SendAsync(new ServerResultAckMessage(ServerResult.ImpossibleToEnterRoom));
            }
        }
        [MessageHandler(typeof(RoomLeaveReqMessage))]
        public void CJoinTunnelInfoReq(GameSession session)
        {
            var gamer = session.Player;
            gamer?.Room?.Leave(gamer, RoomLeaveReason.Left);
        }
        [MessageHandler(typeof(RoomTeamChangeReqMessage))]
        public void CChangeTeamReq(GameSession session, RoomTeamChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || gamer.Room.GameState != GameState.Waiting)
                return;
            try
            {
                gamer.Room.TeamManager.ChangeMode(gamer, message.Mode);
                gamer.Room.TeamManager.ChangeTeam(gamer, message.Team, false);
            }
            catch (RoomException ex)
            {
                _log.ForAccount(gamer).Error(ex, "Failed to change team to {team}", message.Team);
            }
            catch (Exception ex)
            {
                _log.ForAccount(gamer).Error(ex, "Failed to change mode to {mode}", message.Mode);
                gamer.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.Full));
            }
        }
        [MessageHandler(typeof(RoomPlayModeChangeReqMessage))]
        public void CPlayerGameModeChangeReq(GameSession session, RoomPlayModeChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || gamer.Room.GameRuleManager.GameRule.StateMachine.State != GameRuleState.Waiting)
                return;
            try
            {
                gamer.Room.TeamManager.ChangeMode(gamer, message.Mode);
            }
            catch (Exception ex)
            {
                _log.ForAccount(gamer).Error(ex, "Failed to change mode to {mode}", message.Mode);
                gamer.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.Full));
            }
        }
        [MessageHandler(typeof(GameLoadingSuccessReqMessage))]
        public void CLoadingSucceeded(GameSession session)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
                return;
            var phase = gamer.Room?.GameState;
            if (phase != GameState.Loading && phase != GameState.Playing)
                return;
            gamer.RoomInfo.HasLoaded = true;
            gamer.RoomInfo.State = PlayerState.Waiting;
            switch (phase)
            {
                case GameState.Loading:
                    gamer.Room.Broadcast(new RoomGameEndLoadingAckMessage(gamer.Account.Id));
                    break;
                case GameState.Playing:
                    gamer.RoomInfo.State = gamer.RoomInfo.Mode == PlayerGameMode.Normal
                                 ? PlayerState.Alive : PlayerState.Spectating;

                    break;
            }
            if (gamer.Room?.GameRuleManager.GameRule.GameRule != GameRule.Chaser && gamer.Room?.GameRuleManager.GameRule.GameRule != GameRule.Captain && gamer.Room?.GameRuleManager.GameRule.GameRule != GameRule.BattleRoyal)
            {
                var alphaActive = gamer.Room.Players.Values.Where(x => x.RoomInfo.Team.Team == Team.Alpha && x.RoomInfo.State != PlayerState.Lobby);
                var betaActive = gamer.Room.Players.Values.Where(x => x.RoomInfo.Team.Team == Team.Beta && x.RoomInfo.State != PlayerState.Lobby);
                var alphaBenched = gamer.Room._blockplayers.Where(x => x.Value == Team.Alpha);
                var betaBenched = gamer.Room._blockplayers.Where(x => x.Value == Team.Beta);
                if ((alphaActive.Count() - alphaBenched.Count()) > betaActive.Count())
                {
                    var byScore = alphaActive.OrderBy(x => x.RoomInfo.Stats.TotalScore).ToList();
                    var toBench = alphaActive.Count() - betaActive.Count();
                    for (int i = 0; i < toBench; i++)
                    {
                        gamer.Room._blockplayers.TryAdd(gamer, Team.Alpha);
                    }
                }
                if ((betaActive.Count() - betaBenched.Count()) > alphaActive.Count())
                {
                    var byScore = betaActive.OrderBy(x => x.RoomInfo.Stats.TotalScore).ToList();
                    var toBench = betaActive.Count() - alphaActive.Count();
                    for (int i = 0; i < toBench; i++)
                    {
                        gamer.Room._blockplayers.TryAdd(gamer, Team.Beta);
                    }
                }
                if (alphaActive.Count() == betaActive.Count())
                {
                    foreach (var benched in gamer.Room._blockplayers)
                    {
                        gamer.Room._blockplayers.TryRemove(gamer, out var _);
                    }
                }
            }
            gamer.RoomInfo.HasLoaded = true;
            gamer.RoomInfo.State = PlayerState.Waiting;
           // gamer.Room.Broadcast(new RoomGameEndLoadingAckMessage(gamer.Account.Id));
            if (gamer.Room.GameRuleManager.GameRule.GameRule == GameRule.Arcade)
            {
                gamer.Room.Broadcast(new ArcadeSucceedLoadingAckMessage { AccountId = session.Player.Account.Id });
            }
            foreach (var loaded in gamer.Room.Players.Where(x => x.Value.RoomInfo.HasLoaded))
            {
               // gamer.SendAsync(new RoomGameEndLoadingAckMessage(loaded.Value.Account.Id));
                if (gamer.Room.GameRuleManager.GameRule.GameRule == GameRule.Arcade)
                {
                    gamer.SendAsync(new ArcadeSucceedLoadingAckMessage { AccountId = loaded.Value.Account.Id });
                }
            }
            gamer.RoomInfo.State = gamer.RoomInfo.Mode == PlayerGameMode.Spectate
                ? PlayerState.Spectating
                : PlayerState.Alive;

            if (phase == GameState.Playing)
            {
                gamer.Room.GameRuleManager.GameRule.OnBeforeIntrudeSpawn(gamer);
                session.SendAsync(new RoomGameStartAckMessage());
                session.SendAsync(new GameChangeStateAckMessage(gamer.Room.GameState));
                session.SendAsync(new GameRefreshGameRuleInfoAckMessage(gamer.Room.GameState,
                    gamer.Room.GameRuleManager.GameRule.IntrudeTimeState,
                    gamer.Room.GameRuleManager.GameRule.IntrudeRefreshTime));
            }
            gamer.Room.GameRuleManager.GameRule.IntrudeCompleted(gamer);
        }
        [MessageHandler(typeof(RoomIntrudeRoundReq2Message))]
        public void CIntrudeRoundReq2(GameSession session)
        {
            var gamer = session.Player;
            gamer?.Room?.IntrudeRoom(gamer);
        }
        [MessageHandler(typeof(RoomIntrudeRoundReqMessage))]
        public void CIntrudeRoundReq(GameSession session)
        {
            var gamer = session.Player;
            if (gamer == null || gamer?.Room == null)
                return;
            gamer?.Room?.IntrudeRoom(gamer);
        }
        [MessageHandler(typeof(RoomBeginRoundReqMessage))]
        public void CBeginRoundReq(GameSession session)
        {
            var gamer = session.Player;
            if (gamer == null || gamer?.Room == null)
                return;
            gamer?.Room?.BeginRound(gamer);
        }
        [MessageHandler(typeof(RoomBeginRoundReq2Message))]
        public void CBeginRoundReq2(GameSession session)
        {
            var gamer = session.Player;
            if (gamer?.Room == null)
                return;
            gamer?.Room?.BeginRound(gamer);
        }
        [MessageHandler(typeof(RoomReadyRoundReqMessage))]
        public void CReadyRoundReq(GameSession session)
        {
            var gamer = session.Player;
            if (gamer?.Room == null)
                return;
            gamer?.Room?.ChangeReadyStatus(gamer);
        }
        [MessageHandler(typeof(RoomReadyRoundReq2Message))]
        public void CReadyRoundReq2(GameSession session)
        {
            var gamer = session.Player;
            if (gamer == null || gamer?.Room == null)
                return;
            gamer?.Room?.ChangeReadyStatus(gamer);
        }
        [MessageHandler(typeof(ArcadeStageFailedReqMessage))]
        public void ArcadeStageFailedReq(GameSession session, ArcadeStageFailedReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade && gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde))
                return;
            gamer.Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }
        [MessageHandler(typeof(ArcadeStageClearReqMessage))]
        public void ArcadeStageClearReq(GameSession session, ArcadeStageClearReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade && gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde))
                return;
            var gameRoom = gamer.Room;
            if (gamer.Room.GameRuleManager.GameRule.GameRule == GameRule.Arcade)
            {
                ((ArcadeGameRule)gameRoom.GameRuleManager.GameRule).ArcadeStageClear(message.Scores);
            }
            else
                gamer.Room.GameRuleManager.GameRule.StateMachine.Fire(GameRuleStateTrigger.StartResult);
        }
        [MessageHandler(typeof(ArcadeBeginRoundReqMessage))]
        public void ArcadeBeginRoundReq(GameSession session, ArcadeBeginRoundReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade)
                return;
            var gameRoom = gamer.Room;
            ((ArcadeGameRule)gameRoom.GameRuleManager.GameRule).ArcadeStageBegin(session, message.Unk1);
        }
        [MessageHandler(typeof(ArcadeStageSelectReqMessage))]
        public void ArcadeStageSelectReqMessage(GameSession session, ArcadeStageSelectReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade)
                return;
            var gameRoom = gamer.Room;
            ((ArcadeGameRule)gameRoom.GameRuleManager.GameRule).ArcadeStageSelect(session, message.Unk1, message.Unk2);
        }
        [MessageHandler(typeof(ArcadeStageInfoReqMessage))]
        public void ArcadeStageInfoReqMessage(GameSession session, ArcadeStageInfoReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade))
                return;
            gamer.RoomInfo.ArcadeRespawnCount = 10;
            session.SendAsync(new ArcadeRespawnAckMessage { Unk = 10 });
            session.Player.Room.Options.TimeLimit = TimeSpan.FromMilliseconds(message.Unk2);
            session.SendAsync(new ArcadeStageInfoAckMessage(message.Unk1, message.Unk2));
        }
        [MessageHandler(typeof(ArcadeEnablePlayTimeReqMessage))]
        public void ArcadeEnablePlayTimeReqMessage(GameSession session, ArcadeEnablePlayTimeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade))
                return;
            session.SendAsync(new ArcadeEnablePlayTimeAckMessage(message.Unk));
        }
        [MessageHandler(typeof(ArcardRespawnReqMessage))]
        public void ArcardRespawnReqMessage(GameSession session, ArcardRespawnReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade))
                return;
            gamer.RoomInfo.ArcadeRespawnCount--;
            session.Player.Room.GameRuleManager.GameRule.Respawn(session.Player);
            session.SendAsync(new ArcadeRespawnAckMessage { Unk = gamer.RoomInfo.ArcadeRespawnCount });
        }
        [MessageHandler(typeof(ArcadeLoagdingSuccessReqMessage))]
        public void ArcadeLoagdingSuccessReqMessage(GameSession session, ArcadeLoagdingSuccessReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade))
                return;
            session.SendAsync(new ArcadeSucceedLoadingAckMessage { AccountId = session.Player.Account.Id });
        }
        [MessageHandler(typeof(ArcadeStageReadyReqMessage))]
        public void ArcadeStageReadyReqMessage(GameSession session, ArcadeStageReadyReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade))
                return;
            gamer?.Room?.ChangeReadyStatus(gamer);
        }
        [MessageHandler(typeof(ArcadeScoreSyncReqMessage))]
        public void ArcadeScoreSyncReqMessage(GameSession session, ArcadeScoreSyncReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arcade)
                return;
            var gameRoom = gamer.Room;
            ((ArcadeGameRule)gameRoom.GameRuleManager.GameRule).OnArcadeScore(gamer, message.Scores);
        }
        [MessageHandler(typeof(GameEventMessageReqMessage))]
        public void CEventMessageReq(GameSession session, GameEventMessageReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
                return;
            if (message.Event > GameEventMessage.ChaserIn)
                return;

            gamer.Room.Broadcast(new GameEventMessageAckMessage(message.Event, message.AccountId, message.Unk1,
                message.Value, ""));

            if (!gamer.Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing))
                return;
            if (gamer.RoomInfo.State != PlayerState.Lobby)
                return;
            if (!gamer.Room.HasStarted || gamer.RoomInfo.HasLoaded)
                return;
            gamer.Session?.SendAsync(new RoomGameLoadingAckMessage());
        }
        [MessageHandler(typeof(ArenaSpecialPointReqMessage))]
        public void ArenaSpecialPointReq(GameSession session, ArenaSpecialPointReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arena)
                return;
            ((ArenaGameRule)gamer.Room.GameRuleManager.GameRule).OnSpecialPoint(gamer, message.Unk1, message.Unk2);
        }
        [MessageHandler(typeof(ArenaDrawHealthPointAckMessage))]
        public void ArenaDrawHealthPointAck(GameSession session, ArenaDrawHealthPointAckMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arena)
                return;
            ((ArenaGameRule)gamer.Room.GameRuleManager.GameRule).OnDrawHealthPoint(gamer, message);
        }
        [MessageHandler(typeof(RoomItemChangeReqMessage))]
        public void CItemsChangeReq(GameSession session, RoomItemChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null)
                return;
            gamer.Room?.Broadcast(new RoomChangeItemAckMessage(message.Unk1, message.Unk2));
        }
        [MessageHandler(typeof(GameAvatarChangeReqMessage))]
        public void CAvatarChangeReq(GameSession session, GameAvatarChangeReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null )
                return;
            if (gamer.Room?.GameState == GameState.Playing &&
                gamer.Room?.SubGameState != GameTimeState.HalfTime && gamer.RoomInfo.IsReady)
                return;
              gamer.Room?.Broadcast(new GameAvatarChangeAckMessage(message.Unk1, message.Unk2));
        }
        [MessageHandler(typeof(RoomChangeRuleNotifyReqMessage))]
        public void CChangeRuleNotifyReq(GameSession session, RoomChangeRuleNotifyReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
                return;
            if (gamer != gamer.Room.Master)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (gamer.Room.GameState != GameState.Waiting)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            try
            {
                session.Player.Room.ChangeRules(message.Settings);
            }
            catch (Exception)
            {
                session.SendAsync(new RoomChangeRuleFailAckMessage { Result = 1 });
            }
        }
        [MessageHandler(typeof(RoomChangeRuleNotifyReq2Message))]
        public void CChangeRuleNotifyReq2(GameSession session, RoomChangeRuleNotifyReq2Message message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
                return;
            if (gamer != gamer.Room.Master)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            if (gamer.Room.GameState != GameState.Waiting)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                return;
            }
            try
            {
                if (gamer.Channel.Id == 5 &&
                    message.Settings.GameRule != GameRule.Touchdown &&
                    message.Settings.GameRule != GameRule.Deathmatch &&
                    message.Settings.GameRule != GameRule.PassTouchdown)
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game rule not allow {0} does not exist", message.Settings.GameRule);
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (gamer.Channel.Id == 5 && message.Settings.PlayerLimit < 6)
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game low player limit {0} does not exist", message.Settings.PlayerLimit);
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (gamer.Channel.Id == 5 && !string.IsNullOrEmpty(message.Settings.Password))
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game room with a password");
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (gamer.Channel.Id == 5 && (message.Settings.Time > 30 || message.Settings.Time < 20))
                {
                    _log.ForAccount(gamer)
                        .Error("Rank game bad time");
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                if (gamer.Channel.Id == 5 && message.Settings.FMBurnMode > 0)
                {
                    _log.ForAccount(gamer)
                        .Error("Rank FMBurnMode");
                    session.SendAsync(new ServerResultAckMessage(ServerResult.FailedToRequestTask));
                    return;
                }
                _log.ForAccount(gamer).Information(
                    "ChangeRuleNotifyReq2 room={roomId} rawLimit={rawLimit} gameRule={gameRule} map={map}",
                    gamer.Room.Id, message.Settings.PlayerLimit, message.Settings.GameRule, message.Settings.MapId);
                session.Player.Room.ChangeRules2(message.Settings);
            }
            catch (Exception ex)
            {
                _log.Error(ex.ToString());
                session.SendAsync(new RoomChangeRuleFailAckMessage { Result = 1 });
            }
        }
        [MessageHandler(typeof(RoomLeaveReguestReqMessage))]
        public void CLeavePlayerRequestReq(GameSession session, RoomLeaveReguestReqMessage message)
        {
            var gamer = session.Player;
            var gameRoom = gamer.Room;
            if (gameRoom == null)
                return;
            var evicted = gameRoom.Players.GetValueOrDefault(message.AccountId);
            if (evicted == null)
                return;
            switch (message.Reason)
            {
                  case RoomLeaveReason.AFK:
                    if (gamer != evicted)
                        return;
                    break;
                case RoomLeaveReason.Kicked:
                case RoomLeaveReason.ModeratorKick:
                    if ((gameRoom.Master != gamer || gamer.Account.SecurityLevel < SecurityLevel.Tester) &&
                        !gameRoom.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting))
                        return;
                    break;
                default:
                    if (gamer != evicted)
                        return;
                    break;
            }
            gameRoom.Leave(evicted, message.Reason);
        }
        [MessageHandler(typeof(RoomQuickJoinReqMessage))]
        public void QuickJoinReq(GameSession session, RoomQuickJoinReqMessage message)
        {
            var gamer = session.Player;
            if (gamer == null)
                return;
            if (gamer.Room != null)
                return;
            try
            {
                var scoredRooms = new Dictionary<Room, int>();
                foreach (var candidate in gamer.Channel.RoomManager)
                {
                    if (candidate.Options.Password == string.Empty)
                    {
                        if (!candidate.Options.GameRule.Equals((GameRule)message.GameRule))
                            continue;
                        if (!candidate.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Waiting) &&
                            (!candidate.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing) ||
                             candidate.Options.IsNoIntrusion))
                            continue;
                        var weight = 0;
                        weight += Math.Abs(candidate.TeamManager[Team.Alpha].Players.Count() -
                                             candidate.TeamManager[Team.Beta].Players
                                                 .Count());
                        if (candidate.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.SecondHalf))
                        {
                            if (candidate.Options.TimeLimit.TotalSeconds / 2 -
                                candidate.GameRuleManager.GameRule.RoundTime.TotalSeconds <= 15)
                            {
                                weight -= 3;
                            }
                        }
                        scoredRooms.Add(candidate, weight);
                    }
                }
                var ranked = scoredRooms.ToList();
                if (ranked.Any())
                {
                    ranked.Sort((left, right) => right.Value.CompareTo(left.Value));
                    session.SendAsync(new RoomQuickJoinAckMessage(1, (byte)ranked.First().Key.Id));
                    return;
                }
                session.SendAsync(new RoomQuickJoinAckMessage(0, 0));
            }
            catch (Exception)
            {
                session.SendAsync(new ServerResultAckMessage(ServerResult.ServerError));
            }
        }
        [MessageHandler(typeof(TutorialCompletedReqMessage))]
        public void TutorialCompletedReq(GameSession session, TutorialCompletedReqMessage message)
        {
            session.Player.TutorialState = 1;
            session.SendAsync(new TutorialCompletedAckMessage { Unk = 0 });
        }
        [MessageHandler(typeof(Btc_Clear_ReqMessage))]
        public void BtcClearReqMessage(GameSession session, Btc_Clear_ReqMessage message)
        {
            if (session.Player.TutorialState == 0)
            {
                var roll = new SecureRandom();
                var rewards = new List<BTCGiveItemResultDto>();
                 switch (roll.Next(1, 3))
                {
                    case 1:
                        session.Player.Inventory.Create(4020164, 1, 0, new EffectNumber[0], 1);
                        rewards.Add(new BTCGiveItemResultDto(1, 4020164));
                        break;
                    case 2:
                        session.Player.Inventory.Create(4020163, 1, 0, new EffectNumber[0], 1);
                        rewards.Add(new BTCGiveItemResultDto(1, 4020163));
                        break;
                    case 3:
                        session.Player.Inventory.Create(4020162, 1, 0, new EffectNumber[0], 1);
                        rewards.Add(new BTCGiveItemResultDto(1, 4020162));
                        break;
                    default:
                        break;
                }
                session.SendAsync(new Btc_Clear_AckMessage { Unk = 1, Unk2 = rewards.ToArray() });
                session.Player.PEN += 5000;
                session.Player.AP += 2000;
                session.Player.Session.SendAsync(new MoneyRefreshCashInfoAckMessage(session.Player.PEN, session.Player.AP));
                session.Player.TutorialState = 1;
            }
            else
            {
                var rewards = new List<BTCGiveItemResultDto>();
                rewards.Add(new BTCGiveItemResultDto(1, 0));
                session.SendAsync(new Btc_Clear_AckMessage
                {
                    Unk = 1,
                    Unk2 = rewards.ToArray()
                });
            }
            try
            {
                using (var db = AuthDatabase.Open())
                {
                    var existingBan = db.Find<BanDto>(statement => statement
                       .Where($"{nameof(BanDto.AccountId):C} = @{nameof(session.Player.Account.Id)}")
                       .WithParameters(new { session.Player.Account.Id })).FirstOrDefault();
                    if (message.Index == 1 && message.Mode == 3)
                    {
                        session.Channel.DisconnectAsync();
                        if (existingBan == null)
                        {
                            var span = TimeSpan.FromDays(999999);
                            var newBan = new BanDto
                            {
                                AccountId = (int)session.Player.Account.Id,
                                Date = 0,
                                Duration = DateTimeOffset.Now.Add(span).ToUnixTimeSeconds(),
                                Reason = "third party tool"
                            };
                            DbUtil.Insert(db, newBan);
                        }
                    }
                    if (message.Index == 1 && message.Mode == 4)
                    {
                        session.Channel.DisconnectAsync();
                        if (existingBan == null)
                        {
                            var span = TimeSpan.FromDays(999999);
                            var newBan = new BanDto
                            {
                                AccountId = (int)session.Player.Account.Id,
                                Date = 0,
                                Duration = DateTimeOffset.Now.Add(span).ToUnixTimeSeconds(),
                                Reason = "Suspect Process"
                            };
                            DbUtil.Insert(db, newBan);
                        }
                    }
                }
            }
            catch { }
            switch (message.Index)
            {
                case 1:
                    session.Player.TutorialState = 1;
                    session.SendAsync(new Btc_Sync_NoticeMessage());
                    session.SendAsync(new Btc_Clear_AckMessage {});
                    session.SendAsync(new Btc_Sync_NoticeMessage());
                    session.SendAsync(new TutorialCompletedAckMessage { Unk = message.Mode });
                    break;
            }
        }
        [MessageHandler(typeof(GameKickOutRequestReqMessage))]
        public void GameKickOutRequest(GameSession session, GameKickOutRequestReqMessage message)
        {
            const uint voteKickPrice = 2000;
            const int minPlayerRequiredForKick = 4;
            var sender = session.Player;
            var gameRoom = sender.Room;
            if (gameRoom == null)
            {
                sender.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                return;
            }
            if (sender.Room.Players.Count() < minPlayerRequiredForKick)
            {
                sender.SendAsync(new GameKickOutRequestAckMessage { Message = VoteKickMessage.NotEnoughtPlayerToVote });
                return;
            }
            if (gameRoom.VoteKickMgr.State == VoteKickManager.KickState.Execution || gameRoom.VoteKickMgr.State == VoteKickManager.KickState.End)
            {
                sender.SendAsync(new GameKickOutRequestAckMessage { Message = VoteKickMessage.CurrentlyRunning });
                return;
            }
            if (sender.PEN < voteKickPrice)
            {
                sender.SendAsync(new GameKickOutRequestAckMessage { Message = VoteKickMessage.InsufficientMoney });
                return;
            }
            var accused = gameRoom.Players.FirstOrDefault(x => x.Value.Account.Id == message.Target).Value;
            if (accused == null)
            {
                sender.SendAsync(new GameKickOutRequestAckMessage { Message = VoteKickMessage.PlayerNotInRoom });
                return;
            }
            if (accused.Account.SecurityLevel > SecurityLevel.Tester)
            {
                sender.SendAsync(new GameKickOutRequestAckMessage { Message = VoteKickMessage.CantKickGM });
                return;
            }
            sender.PEN -= voteKickPrice;
            sender.SendAsync(new MoneyRefreshPenInfoAckMessage { Unk = sender.PEN });
            gameRoom.VoteKickMgr.Start(sender, accused, message.Reason);
        }
        [MessageHandler(typeof(GameKickOutVoteResultReqMessage))]
        public void GameKickOutVoteResultRequest(GameSession session, GameKickOutVoteResultReqMessage message)
        {
            var gamer = session.Player;
            var gameRoom = gamer.Room;
            if (gameRoom == null)
            {
                gamer.SendAsync(new ServerResultAckMessage(ServerResult.CannotFindRoom));
                return;
            }
            if (gameRoom.VoteKickMgr.State == VoteKickManager.KickState.Execution)
            {
                gameRoom.VoteKickMgr.UpdateResult(message.IsYes);
                gamer.SendAsync(new GameKickOutVoteResultAckMessage { Result = VoteKickResult.Ok });
            }
        }
        [MessageHandler(typeof(InGameItemGetReqMessage))]
        public void InGameItemGetReq(GameSession session, InGameItemGetReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde)
                return;
            gamer.Room.Broadcast(new InGameItemGetAckMessage
            {
                Unk1 = (long)gamer.Account.Id,
                Unk2 = message.Unk1,
                Unk3 = message.Unk2
            });
        }
        [MessageHandler(typeof(InGameItemDropReqMessage))]
        public void InGameItemDropReq(GameSession session, InGameItemDropReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Horde)
                return;
            var conquestRule = (ConquestGameRule)gamer.Room.GameRuleManager.GameRule;
            var roll = new SecureRandom().Next(0, 10);
            var itemKind = 0;
            var itemPayload = 0L;
            if (roll < 8)
            {
                itemKind = 319717609;
                itemPayload = 28154369870397440;
            }
            else
            {
                itemKind = 319786968;
                itemPayload = 28154369635516416;
            }
            var drop = new InGameItemDropAckMessage
            {
                Item = new ItemDropAckDto
                {
                    Counter = conquestRule.DropCount++,
                    Unk2 = 3,
                    Unk3 = 2,
                    Unk4 = itemKind,
                    Position = message.Item.Position,
                    Unk6 = itemPayload
                }
            };
            gamer.Room.Broadcast(drop);
        }
         [MessageHandler(typeof(Record_Burning_Data))]
        public void RecordBurningData(GameSession session, Record_Burning_Data message)
        {
        }
        [MessageHandler(typeof(UseBurningBuff_Req))]
        public void UseBurningBuffReq(GameSession session, UseBurningBuff_Req Message)
        {
        }
        [MessageHandler(typeof(MoneyUseCoinReqMessage))]
        public void MoneyUseCoinRequest(GameSession session, MoneyUseCoinReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.PlayerCoinBuff != null)
            { gamer.PlayerCoinBuff.StartBuffSystem(message.BuffType); }
        }
        [MessageHandler(typeof(ArenaSetGameOptionReqMessage))]
        public void ArenaSetGameOptionReq(GameSession session, ArenaSetGameOptionReqMessage message)
        {
            var gamer = session.Player;
            if (gamer?.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Arena)
                return;
            ((ArenaGameRule)gamer.Room.GameRuleManager.GameRule).OnSetGameOption(gamer, message.Unk);
        }
        #region Scores
        [MessageHandler(typeof(ScoreAIKillReqMessage))]
        public void ScoreAIKillReqMessage(GameSession session, ScoreAIKillReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted)
                return;
            var gameRoom = gamer.Room;
            ((WarfareGameRule)gameRoom.GameRuleManager.GameRule).OnScoreAIKill(gamer, message.Unk[0]);
        }
        [MessageHandler(typeof(ScoreKillReqMessage))]
        public void CScoreKillReq(GameSession session, ScoreKillReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            gameRoom.GameRuleManager.GameRule.OnScoreKill(slayer, null, victim, message.Score.Weapon, message.Score.Target,
                message.Score.Killer, null);
        }
        [MessageHandler(typeof(ScoreKillAssistReqMessage))]
        public void CScoreKillAssistReq(GameSession session, ScoreKillAssistReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            if (message.Score == null || message.Score.Killer == null || message.Score.Assist == null)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            var helper = gameRoom.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (helper != null && helper.RoomInfo.PeerId.EqualSlot(message.Score.Assist))
                helper.RoomInfo.PeerId = message.Score.Assist;
            gameRoom.GameRuleManager.GameRule.OnScoreKill(slayer, helper, victim, message.Score.Weapon,
                message.Score.Target,
                message.Score.Killer, message.Score.Assist);
        }
        [MessageHandler(typeof(ScoreOffenseReqMessage))]
        public void CScoreOffenseReq(GameSession session, ScoreOffenseReqMessage message)
        {
            var gamer = session.Player;
            var gameRoom = gamer.Room;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
            }
        }
        [MessageHandler(typeof(ScoreOffenseAssistReqMessage))]
        public void CScoreOffenseAssistReq(GameSession session, ScoreOffenseAssistReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            var helper = gameRoom.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (helper != null && helper.RoomInfo.PeerId.EqualSlot(message.Score.Assist))
                helper.RoomInfo.PeerId = message.Score.Assist;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreOffense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
            }
        }
        [MessageHandler(typeof(ScoreDefenseReqMessage))]
        public void CScoreDefenseReq(GameSession session, ScoreDefenseReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, null, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, null);
                    break;
            }
        }
        [MessageHandler(typeof(ScoreDefenseAssistReqMessage))]
        public void CScoreDefenseAssistReq(GameSession session, ScoreDefenseAssistReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            var helper = gameRoom.Players.GetValueOrDefault(message.Score.Assist.AccountId);
            if (helper != null && helper.RoomInfo.PeerId.EqualSlot(message.Score.Assist))
                helper.RoomInfo.PeerId = message.Score.Assist;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreDefense(slayer, helper, victim,
                        message.Score.Weapon, message.Score.Target, message.Score.Killer, message.Score.Assist);
                    break;
            }
        }
        [MessageHandler(typeof(ScoreTeamKillReqMessage))]
        public void CScoreTeamKillReq(GameSession session, ScoreTeamKillReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Score.Target.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Score.Target))
                victim.RoomInfo.PeerId = message.Score.Target;
            var slayer = gameRoom.Players.GetValueOrDefault(message.Score.Killer.AccountId);
            if (slayer != null && slayer.RoomInfo.PeerId.EqualSlot(message.Score.Killer))
                slayer.RoomInfo.PeerId = message.Score.Killer;
            gameRoom.GameRuleManager.GameRule.OnScoreKill(slayer, null, victim, message.Score.Weapon, message.Score.Target,
                message.Score.Killer, null);
        }
        [MessageHandler(typeof(ScoreHealAssistReqMessage))]
        public void CScoreHealAssistReq(GameSession session, ScoreHealAssistReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || !gamer.Room.HasStarted|| gamer.RoomInfo.State != PlayerState.Alive)
                return;
            var gameRoom = gamer.Room;
            var healed = gameRoom.Players.GetValueOrDefault(message.Id.AccountId);
            if (healed != null && healed.RoomInfo.PeerId.EqualSlot(message.Id))
                healed.RoomInfo.PeerId = message.Id;
            gameRoom.GameRuleManager.GameRule.OnScoreHeal(healed, message.Id);
        }
        [MessageHandler(typeof(ScoreSuicideReqMessage))]
        public void CScoreSuicideReq(GameSession session, ScoreSuicideReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
            {
                session.SendAsync(new InGamePlayerResponseOfDeathAckMessage());
                return;
            }
            if (!gamer.Room.HasStarted)
                return;
            var gameRoom = gamer.Room;
            var victim = gameRoom.Players.GetValueOrDefault(message.Id.AccountId);
            if (victim != null && victim.RoomInfo.PeerId.EqualSlot(message.Id))
                victim.RoomInfo.PeerId = message.Id;
            gameRoom.GameRuleManager.GameRule.OnScoreSuicide(victim, message.Id, (AttackAttribute)message.Icon);
        }
        [MessageHandler(typeof(ScoreReboundReqMessage))]
        public void CScoreReboundReq(GameSession session, ScoreReboundReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
            {
                session.SendAsync(new ScoreReboundAckMessage(message.NewId, message.OldId));
                return;
            }
            if (!gamer.Room.HasStarted)
                return;
            var gameRoom = gamer.Room;
            var previousHolder = gameRoom.Players.GetValueOrDefault(message.OldId.AccountId);
            if (previousHolder != null && previousHolder.RoomInfo.PeerId.EqualSlot(message.OldId))
                previousHolder.RoomInfo.PeerId = message.OldId;
            var newHolder = gameRoom.Players.GetValueOrDefault(message.NewId.AccountId);
            if (newHolder != null && newHolder.RoomInfo.PeerId.EqualSlot(message.NewId))
                newHolder.RoomInfo.PeerId = message.NewId;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreRebound(newHolder, previousHolder, message.NewId,
                        message.OldId);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreRebound(newHolder, previousHolder,
                        message.NewId, message.OldId);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreRebound(newHolder, previousHolder,
                        message.NewId, message.OldId);
                    break;
            }
        }
        [MessageHandler(typeof(ScoreGoalReqMessage))]
        public void CScoreGoalReq(GameSession session, ScoreGoalReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null)
            {
                session.SendAsync(new ScoreGoalAckMessage(message.PeerId));
                return;
            }
            if (!gamer.Room.HasStarted)
                return;
            var gameRoom = gamer.Room;
            var scorer = gameRoom.Players.GetValueOrDefault(message.PeerId.AccountId);
            if (scorer != null && scorer.RoomInfo.PeerId.EqualSlot(message.PeerId))
                scorer.RoomInfo.PeerId = message.PeerId;
            switch (gameRoom.Options.GameRule)
            {
                case GameRule.Touchdown:
                    ((TouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreGoal(scorer, message.PeerId);
                    break;
                case GameRule.PassTouchdown:
                    ((PassTouchdownGameRule)gameRoom.GameRuleManager.GameRule).OnScoreGoal(scorer, message.PeerId);
                    break;
                case GameRule.CombatTrainingTD:
                    ((TouchdownTrainingGameRule)gameRoom.GameRuleManager.GameRule).OnScoreGoal(scorer, message.PeerId);
                    break;
            }
        }
        [MessageHandler(typeof(SlaughterAttackPointReqMessage))]
        public void SlaughterAttackPointReq(GameSession session, SlaughterAttackPointReqMessage message)
        {
            var gameRoom = session.Player?.Room;
            if (gameRoom?.GameRuleManager.GameRule.GameRule != GameRule.Chaser)
                return;
            var struck = gameRoom.Players.GetValueOrDefault(message.AccountId);
            ((ChaserGameRule)gameRoom.GameRuleManager.GameRule).OnScoreAttack(struck, message.Unk1, message.Unk2);
        }
        [MessageHandler(typeof(SlaughterHealPointReqMessage))]
        public void SlaughterHealPointReq(GameSession session, SlaughterHealPointReqMessage message)
        {
            session.SendAsync(new SlaughterHealPointReqMessage()
            {
                Unk = message.Unk,
            });
        }
        [MessageHandler(typeof(ScoreMissionScoreReqMessage))]
        public void ScoreMissionScoreReq(GameSession session, ScoreMissionScoreReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room == null || gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Practice || gamer.RoomInfo.State != PlayerState.Alive)
                return;
            session.SendAsync(
                new ScoreMissionScoreAckMessage { AccountId = session.Player.Account.Id, Score = message.Score });
        }
        [MessageHandler(typeof(SeizePositionCaptureReqMessage))]
        public void SeizePositionCapture(GameSession session, SeizePositionCaptureReqMessage message)
        {
            var gamer = session.Player;
            if (gamer.Room.GameRuleManager.GameRule.GameRule != GameRule.Siege)
                return;
            ((SiegeGameRule)gamer.Room.GameRuleManager.GameRule).OnCapture(gamer, message.Base, message.IsCapturing, message.Distance);
        }
         [MessageHandler(typeof(SeizeBuffItemGainReqMessage))]
          public void SeizeBuffItemGain(GameSession session, SeizeBuffItemGainReqMessage message)
          {
            var gamer = session.Player;
            var rule = gamer.Room?.GameRuleManager.GameRule;
            if (rule == null)
                return;
            if (rule.GameRule == GameRule.Siege)
                ((SiegeGameRule)rule).OnPickup(gamer, message.Item, session);
            else if (rule.GameRule == GameRule.Arcade)
                gamer.Room.Broadcast(new SeizeBuffItemGainAckMessage { PickupID = gamer.RoomInfo.PeerId, PlayerID = message.Item });
        }
        #endregion
    }
}

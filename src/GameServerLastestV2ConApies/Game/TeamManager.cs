using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Collections.Concurrent;
using Santana.Network;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Message.GameRule;
namespace Santana.Game
{
    internal class TeamManager : IReadOnlyDictionary<Team, PlayerTeam>
    {
        private readonly ConcurrentDictionary<Team, PlayerTeam> _teams = new ConcurrentDictionary<Team, PlayerTeam>();
        private readonly ConcurrentDictionary<Player, Team> _players = new ConcurrentDictionary<Player, Team>();
        public int MaxPlayerLimit => (int)_teams.Sum(x => x.Value.PlayerLimit + x.Value.SpectatorLimit);
        public int CurPlayerCount => (int)_teams.Sum(x => x.Value.Players.Count());
        public EventHandler<TeamChangedEventArgs> TeamChanged;
        public Room Room { get; }
        public IEnumerable<Player> Players => _players.Select(x => x.Key);
        public IEnumerable<Player> PlayersPlaying => _teams.Values.SelectMany(team => team.PlayersPlaying);
        public IEnumerable<Player> Spectators => _teams.Values.SelectMany(team => team.Spectators);
        public IEnumerable<Player> NoSpectatorPlayers => _teams.Values.SelectMany(team => team.NoSpectatorPlayers);
        public TeamManager(Room room)
        {
            Room = room;
        }
        protected virtual void OnTeamChanged(PlayerTeam from, PlayerTeam to, Player plr)
        {
            TeamChanged?.Invoke(this, new TeamChangedEventArgs(from, to, plr));
        }
        public void Add(Team team, uint playerLimit, uint spectatorLimit)
        {
            var playerTeam = new PlayerTeam(this, team, playerLimit, spectatorLimit);
            if (!_teams.TryAdd(team, playerTeam))
                throw new Exception($"Team {team} already exists");
        }
        public void Remove(Team team)
        {
            if (_teams.ContainsKey(team))
            {
                if (_teams.TryGetValue(team, out var selTeam))
                {
                    foreach (var plr in selTeam.Keys)
                        Leave(plr);
                    _teams.Remove(team);
                }
            }
        }
        public void JoinDirectly(Player plr, Team team)
        {
            if (!ContainsKey(team) || _players.ContainsKey(plr))
                return;
            if (_players.TryAdd(plr, team))
                this[team].Intern_Join(plr, false);
        }
          public void Join(Player plr)
          {
              try
              {
                  var teams = _teams.Values
                      .Where(t => t.PlayerLimit > 0 && t.Players.Count() < t.PlayerLimit + t.SpectatorLimit).ToArray();
                  var min = (uint)teams.Min(t => t.Count);
                  teams = teams.Where(t => t.Count == min).ToArray();
                  min = teams.Min(t => t.Score);
                  teams = teams.Where(t => t.Score == min).ToArray();
                  var team = teams[0];
                  if (!_players.ContainsKey(plr) && _players.TryAdd(plr, team.Team))
                  {
                    team.Intern_Join(plr, false);
                  }
                  else
                  {
                      throw new RoomException("Player is already in a Team");
                  }
              }
              catch (Exception ex) { }
          }
        public void Leave(Player plr)
        {
            if (_players.ContainsKey(plr))
            {
                if (_players.TryRemove(plr, out var team))
                {
                    _teams[team].Intern_Leave(plr);
                }
            }
        }
        public void ChangeTeam(Player plr, Team team, bool allowready)
        {
            if (_players.TryGetValue(plr, out var currenTeamVal))
            {
                if (Room.GameRuleManager.GameRule.StateMachine.IsInState(GameRuleState.Playing))
                    throw new RoomException("Game is running");
                if (!allowready && plr.RoomInfo.IsReady)
                {
                    plr.Session.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.AlreadyReady));
                    return;
                }
                if (!_teams.TryGetValue(currenTeamVal, out var currentTeam))
                    throw new RoomException($"Player is in a room, but not inside a team");
                if (!_teams.TryGetValue(team, out var targetTeam))
                    throw new RoomException($"Invalid team {team}");
                try
                {
                    currentTeam.Intern_Leave(plr);
                    targetTeam.Intern_Join(plr, true);
                    _players.TryUpdate(plr, team, currenTeamVal);
                    OnTeamChanged(currentTeam, targetTeam, plr);
                }
                catch (TeamLimitReachedException)
                {
                    currentTeam.Intern_Join(plr, false);
                    plr.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.Full));
                }
            }
            else
            {
                throw new RoomException("Player is not inside this room");
            }
        }
        public void ChangeMode(Player plr, PlayerGameMode mode)
        {
            if (mode == PlayerGameMode.Ignore)
                return;
            if (_players.TryGetValue(plr, out var currenTeamVal))
            {
                if (plr.RoomInfo.Mode == mode)
                    return;
                if (plr.RoomInfo.HasLoaded)
                    throw new RoomException("Player is playing");
                if (plr.RoomInfo.IsReady)
                {
                    plr.Session.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.AlreadyReady));
                    return;
                }
                if (!_teams.TryGetValue(currenTeamVal, out var currentTeam))
                    throw new RoomException($"Player is in a room, but not inside a team");
                switch (mode)
                {
                    case PlayerGameMode.Normal:
                        if (currentTeam.NoSpectatorPlayers.Count() >= currentTeam.PlayerLimit)
                        {
                            plr.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.Full));
                            return;
                        }
                        break;
                    case PlayerGameMode.Spectate:
                        if (currentTeam.Spectators.Count() >= currentTeam.SpectatorLimit)
                        {
                            plr.SendAsync(new RoomChangeTeamFailAckMessage(ChangeTeamResult.Full));
                            return;
                        }
                        break;
                    default:
                        throw new RoomException($"Invalid mode {mode}");
                }
                plr.RoomInfo.Mode = mode;
                Broadcast(new RoomPlayModeChangeAckMessage(plr.Account.Id, mode));
            }
            else
            {
                throw new RoomException("Player is not inside this room");
            }
        }
        public int PlayerIndex(Player plr)
        {
            return PlayersPlaying.ToList().IndexOf(plr);
        }
        #region Broadcast
        public void Broadcast(object message)
        {
            foreach (var team in _teams.Values)
                team.Broadcast(message);
        }
        #endregion
        #region IReadOnlyDictionary
        public int Count => _teams.Count;
        public IEnumerable<Team> Keys => _teams.Keys;
        public IEnumerable<PlayerTeam> Values => _teams.Values;
        public PlayerTeam this[Team key]
        {
            get
            {
                PlayerTeam team;
                TryGetValue(key, out team);
                return team;
            }
        }
        public bool ContainsKey(Team key)
        {
            return _teams.ContainsKey(key);
        }
        public bool TryGetValue(Team key, out PlayerTeam value)
        {
            return _teams.TryGetValue(key, out value);
        }
        public IEnumerator<KeyValuePair<Team, PlayerTeam>> GetEnumerator()
        {
            return _teams.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
    internal class PlayerTeam : IReadOnlyDictionary<Player, byte>
    {
        private readonly ConcurrentDictionary<Player, byte> _players = new ConcurrentDictionary<Player, byte>();
        public PlayerTeam(TeamManager teamManager, Team team, uint playerLimit, uint spectatorLimit)
        {
            TeamManager = teamManager;
            Team = team;
            PlayerLimit = playerLimit;
            SpectatorLimit = spectatorLimit;
        }
        public TeamManager TeamManager { get; }
        public Team Team { get; }
        public uint PlayerLimit { get; set; }
        public uint SpectatorLimit { get; set; }
        public uint Score { get; set; }
        public IEnumerable<Player> PlayersPlaying =>
            _players.Keys.Where(plr => plr.RoomInfo.State != PlayerState.Lobby && plr.RoomInfo.HasLoaded);
        public IEnumerable<Player> Players => _players.Keys;
        public IEnumerable<Player> NoSpectatorPlayers =>
            _players.Keys.Where(plr => plr.RoomInfo.Mode == PlayerGameMode.Normal);
        public IEnumerable<Player> Spectators =>
            _players.Keys.Where(plr => plr.RoomInfo.Mode == PlayerGameMode.Spectate);
        public void Intern_Join(Player plr, bool isChange)
        {
            try
            {
                if (plr?.Room == null)
                    throw new RoomException("Player not in room");
                if (plr.RoomInfo.Team == this)
                    throw new RoomException("Actor is already in this team");
                if (plr.RoomInfo.Mode == PlayerGameMode.Normal)
                {
                    if (NoSpectatorPlayers.Count() >= PlayerLimit)
                        throw new TeamLimitReachedException();
                }
                else
                {
                    if (Spectators.Count() >= SpectatorLimit)
                        throw new TeamLimitReachedException();
                }
                if (_players.TryAdd(plr, plr.RoomInfo.Slot))
                {
                    plr.RoomInfo.Team = this;
                    if (isChange)
                        TeamManager.Broadcast(new RoomChangeTeamAckMessage(plr.Account.Id, Team, plr.RoomInfo.Mode));
                }
            }
            catch (Exception ex) { }
        }
        internal void Intern_Leave(Player plr)
        {
            if (plr != null && plr.RoomInfo.Team != null)
                if (_players.TryRemove(plr, out var slot))
                {
                    plr.RoomInfo.Team = null;
                }
        }
        #region Broadcast
        public void Broadcast(object message)
        {
            foreach (var plr in _players.Keys)
                plr.SendAsync(message);
        }
        #endregion
        #region IReadOnlyDictionary
        public int Count => _players.Count;
        public IEnumerable<Player> Keys => _players.Keys;
        public IEnumerable<byte> Values => _players.Values;
        public byte this[Player key]
        {
            get { return _players.FirstOrDefault(x => x.Key == key).Value; }
        }
        public Player this[byte key]
        {
            get { return _players.FirstOrDefault(x => x.Value == key).Key; }
        }
        public bool ContainsKey(Player key)
        {
            return _players.ContainsKey(key);
        }
        public bool TryGetValue(Player key, out byte value)
        {
            return _players.TryGetValue(key, out value);
        }
        public IEnumerator<KeyValuePair<Player, byte>> GetEnumerator()
        {
            return _players.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        #endregion
    }
}

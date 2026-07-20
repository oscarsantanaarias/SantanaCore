namespace Santana.Game.GameRules
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SantanaLib.Threading.Tasks;
    using MySqlConnector;
    using Santana;
    using Santana.Network;
    using Santana.Network.Data.GameRule;
    using Santana.Network.Message.Chat;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using static System.Collections.Specialized.BitVector32;
    internal class SiegeBaseDto
    {
        public Team Owner = Team.Neutral;
        public Player Capturer = null;
        public List<Player> PlayerCloser = new List<Player>();
        public List<ulong> Drops = new List<ulong>();
        public Player PlayerWhoCapturedForUpKeep = null;
    }
    internal class SiegeItemDto
    {
        public ulong Item { get; set; }
        public ushort Base { get; set; }
    }
    internal class SiegeGameRule : GameRuleBase
    {
        public SiegeGameRule(Room room)
            : base(room)
        {
            Briefing = new SiegeBriefing(this);
            StateMachine.Configure(GameRuleState.Waiting)
                .PermitIf(GameRuleStateTrigger.StartPrepare, GameRuleState.Preparing, CanStartGame);
            StateMachine.Configure(GameRuleState.Preparing)
                .Permit(GameRuleStateTrigger.StartGame, GameRuleState.FullGame);
            StateMachine.Configure(GameRuleState.FullGame)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.EnteringResult);
            StateMachine.Configure(GameRuleState.EnteringResult)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.StartResult, GameRuleState.Result);
            StateMachine.Configure(GameRuleState.Result)
                .SubstateOf(GameRuleState.Playing)
                .Permit(GameRuleStateTrigger.EndGame, GameRuleState.Waiting);
        }
        private readonly SiegeBaseDto _siteAlpha = new SiegeBaseDto();
        private readonly SiegeBaseDto _siteBeta = new SiegeBaseDto();
        private readonly SiegeBaseDto _siteGamma = new SiegeBaseDto();
        private List<SiegeBaseDto> _sites;
        private readonly List<SiegeItemDto> _pendingItems = new List<SiegeItemDto>();
        private readonly List<ulong> _liveDrops = new List<ulong>();
        public TaskLoop _worker;
        public TaskLoop _dropWorker;
        private const int DropIntervalSeconds = 20;
        private const int DropsPerSite = 2;
        private const int MaxLiveDropsPerSite = 6;
        private uint _captureFlag;
        private uint _assistScore;
        private uint _captureScore;
        private uint _upkeepSeconds = 240;
        private uint _dropSeq;
        public override GameRule GameRule => GameRule.Siege;
        public override Briefing Briefing { get; }
        public override bool CountMatch => true;
        public SiegeBriefing GetBriefing()
        {
            return (SiegeBriefing)Briefing;
        }
        public override void Initialize()
        {
            var perTeamPlayers = (uint)Room.Options.PlayerLimit / 2;
            var perTeamSpectators = (uint)Room.Options.SpectatorLimit / 2;
            Room.TeamManager.Add(Team.Alpha, perTeamPlayers, perTeamSpectators);
            Room.TeamManager.Add(Team.Beta, perTeamPlayers, perTeamSpectators);
            _sites = new List<SiegeBaseDto> { _siteAlpha, _siteBeta, _siteGamma };
            _worker = new TaskLoop(TimeSpan.FromSeconds(_upkeepSeconds), Worker);
            _dropWorker = new TaskLoop(TimeSpan.FromSeconds(DropIntervalSeconds), DropWorker);
            base.Initialize();
        }
        public override void Cleanup()
        {
            if (_worker != null)
                _worker.Stop();
            if (_dropWorker != null)
                _dropWorker.Stop();
            Room.TeamManager.Remove(Team.Alpha);
            Room.TeamManager.Remove(Team.Beta);
            base.Cleanup();
        }
        public override void Reload()
        {
            _siteAlpha.Owner = Team.Neutral;
            _siteBeta.Owner = Team.Neutral;
            _siteGamma.Owner = Team.Neutral;
            _siteAlpha.Capturer = null;
            _siteBeta.Capturer = null;
            _siteGamma.Capturer = null;
            _siteAlpha.PlayerWhoCapturedForUpKeep = null;
            _siteBeta.PlayerWhoCapturedForUpKeep = null;
            _siteGamma.PlayerWhoCapturedForUpKeep = null;
            _pendingItems.Clear();
            _upkeepSeconds = 60;
        }
        public bool ValidPlayer(Player plr)
        {
            if (plr == null)
                return false;
            if (plr.Room != Room)
                return false;
            if (!plr.RoomInfo.HasLoaded)
                return false;
            return true;
        }
        public override void OnIntrudeCompleted(Player plr)
        {
            if (_sites != null)
            {
                var infos = new List<SeizeIntrudeInfoDto>();
                ushort siteId = 1;
                foreach (var site in _sites)
                {
                    infos.Add(new SeizeIntrudeInfoDto
                    {
                        Base = siteId,
                        BaseOwner = (byte)site.Owner,
                        Percentage = (ushort)(site.Owner != Team.Neutral ? 30000 : 0),
                        PercentageGoal = 30000,
                        Unk1 = 0
                    });
                    siteId++;
                }
                plr.SendAsync(new SeizeUpdateInfoByIntrudeAckMessage(infos.ToArray()));
            }
            if (_liveDrops.Count > 0)
                plr.SendAsync(new SeizeDropBuffItemAckMessage { Pickups = _liveDrops.ToArray() });

            // El marcador de equipos y la columna Capture se arman SUMANDO el Unk1 de cada
            // SeizeUpdateInfoAck en vivo. El que entra tarde se los perdio todos y ningun otro
            // mensaje se los manda (el ByIntrude solo lleva dueño y % de las bases), asi que
            // arranca en 0-0. Le reproducimos el acumulado, uno por jugador con puntos.
            var seedInfos = new List<SeizeUpdateInfoDto>();
            foreach (var other in Room.TeamManager.Players)
            {
                var record = GetRecord(other);
                if (record == null || record.CapturePoints == 0)
                    continue;
                // Base tiene que ser una base VALIDA (1..3) o el cliente descarta el DTO entero
                // y la siembra no aplica. Mandamos la 1 con su estado REAL, asi el paquete es
                // valido y a la vez no mueve ninguna bandera.
                // Se imita un paquete de captura REAL (IsCaptured = equipo, Percentage al tope):
                // con IsCaptured = 0 el cliente ignora el Unk1 y el marcador seguia en 0.
                seedInfos.Add(new SeizeUpdateInfoDto
                {
                    Base = 1,
                    IsCaptured = (uint)other.RoomInfo.Team.Team,
                    CurrentCaptureTeam = (byte)other.RoomInfo.Team.Team,
                    BaseOwner = (byte)_sites[0].Owner,
                    Percentage = 30000,
                    PercentageGoal = 30000,
                    CapturePoints = 0,
                    AssistPoints = 0,
                    Unk1 = (int)record.CapturePoints,
                    Player = other.Account.Id,
                    Assists = Array.Empty<ulong>(),
                    Points = 0
                });
            }
            if (seedInfos.Count > 0)
                plr.SendAsync(new SeizeUpdateInfoAckMessage { Infos = seedInfos.ToArray() });
        }
        public override void Update(TimeSpan delta)
        {
            base.Update(delta);
            var teamMgr = Room.TeamManager;
            try
            {
                if (Room.GameState == GameState.Playing &&
                    !StateMachine.IsInState(GameRuleState.EnteringResult) &&
                    !StateMachine.IsInState(GameRuleState.Result) &&
                    RoundTime >= TimeSpan.FromSeconds(5))
                {
                    var lowestHeadcount = teamMgr.Values.Min(team =>
                        team.Keys.Count(plr => plr.RoomInfo.HasLoaded));
                    var limit = TimeSpan.FromMilliseconds(Room.Options.TimeLimit.TotalMilliseconds);
                    if (RoundTime >= limit)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    if (lowestHeadcount == 0 && !Room.Options.IsFriendly)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    if (Room.Options.IsFriendly && Room.GameState == GameState.Playing &&
                        teamMgr.NoSpectatorPlayers.Count() == 0)
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    if (teamMgr.PlayersPlaying.Any(plr => plr.RoomInfo.Stats.TotalScore >= Room.Options.ScoreLimit))
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    if (teamMgr.Values.Any(team => team.Score >= Room.Options.ScoreLimit))
                        StateMachine.Fire(GameRuleStateTrigger.StartResult);
                    if (Room.GameState == GameState.Playing && !_worker.IsRunning)
                        _worker.Start();
                    if (Room.GameState == GameState.Result && _worker.IsRunning)
                        _worker.Stop();
                    if (Room.GameState == GameState.Playing && !_dropWorker.IsRunning)
                        _dropWorker.Start();
                    if (Room.GameState == GameState.Result && _dropWorker.IsRunning)
                        _dropWorker.Stop();
                }
            }
            catch (Exception e)
            {
                Room.Logger.Error(e.ToString());
            }
        }
        private List<ulong> ItemsGenerate(ushort Base)
        {
            var rng = new Random();
            var generated = new List<ulong>();
            foreach (var buff in new[] { (nibble: 1ul, amount: 30ul), (nibble: 2ul, amount: 2000ul), (nibble: 3ul, amount: 5ul), (nibble: 4ul, amount: 5ul), (nibble: 9ul, amount: 100ul), (nibble: 0xBul, amount: 1ul) })
            {
                ulong inst = (ulong)(++_dropSeq & 0xFF);
                ulong lowbits = ((ulong)rng.Next(1, 0xFFFF) << 8) | (Base & 0xFFul);
                generated.Add((inst << 48) | (buff.amount << 36) | (buff.nibble << 24) | lowbits);
            }
            return generated;
        }
        public void SpawnPickups(ushort _base, int count = 1)
        {
            var batch = new List<ulong>();
            for (uint i = 0; i < count; i++)
            {
                ulong drop = ItemsGenerate(_base).ShuffleSecure().ToArray()[0];
                batch.Add(drop);
                _liveDrops.Add(drop);
            }
            Room.Broadcast(new SeizeDropBuffItemAckMessage
            {
                Pickups = batch.ToArray()
            });
        }
        public void OnPickup(Player plr, ulong pickup, GameSession session)
        {
            if (!_liveDrops.Contains(pickup))
                return;
            _liveDrops.Remove(pickup);
            GetRecord(plr).ObtainedItems++;
            plr.stats.GetSiegeStats().ItemObtainScore++;
            var type = (int)((pickup >> 24) & 0xF);
            Room.Broadcast(new SeizeBuffItemGainAckMessage
            {
                PickupID = plr.Account.Id,
                PlayerID = pickup
            });
            if (type == 3)
            {
                plr.PEN += 5;
                plr.Session.SendAsync(new MoneyRefreshCashInfoAckMessage(plr.PEN, plr.AP));
                plr.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, plr.Account.Id, "System", "You have Earned 5 PEN"));
            }
            else if (type == 4)
            {
                plr.TotalExperience += 5;
                plr.ChatSession.SendAsync(new MessageChatAckMessage(ChatType.Channel, plr.Account.Id, "System", "You have Earned 5 Exp"));
            }
            else if (type == 0xB)
            {
                // El verde de la bandera: +1 al score de la partida. Mismo nibble que usa el
                // upkeep para su flag (0x100B0000 -> (>>24)&0xF == 0xB).
                plr.RoomInfo.Team.Score++;
            }
        }
        // Los drops van en su propio loop: el upkeep corre cada _upkeepSeconds y no queremos
        // atar la frecuencia de las fichas al ritmo con que se reparten los puntos.
        private async Task DropWorker(TimeSpan delta)
        {
            if (_sites == null)
                return;
            ushort siteId = 1;
            foreach (var site in _sites)
            {
                if (site.Owner != Team.Neutral)
                {
                    // El id del drop lleva la base en el byte bajo (ver ItemsGenerate), asi se
                    // cuenta cuantas quedan sin recoger en este sitio.
                    var live = _liveDrops.Count(d => (d & 0xFF) == siteId);
                    if (live < MaxLiveDropsPerSite)
                        SpawnPickups(siteId, Math.Min(DropsPerSite, MaxLiveDropsPerSite - live));
                }
                siteId++;
            }
            await Task.CompletedTask;
        }
        private async Task Worker(TimeSpan delta)
        {
            if (RoundTime.Seconds < 15)
                return;
            ushort siteId = 1;
            foreach (var site in _sites)
            {
                if (site.Owner != Team.Neutral)
                {
                    ulong flag = Convert.ToUInt64(string.Format("{0:X2}{1:X2}", 0x100B0000, siteId), 16);
                    Room.Broadcast(new SeizeBuffItemGainAckMessage
                    {
                        PlayerID = site.PlayerWhoCapturedForUpKeep.Account.Id,
                        PickupID = flag
                    });
                    site.PlayerWhoCapturedForUpKeep.RoomInfo.Team.Score++;
                    // NO sumar aca a CapturePoints: la columna Capture del cliente NO cuenta el
                    // SeizeBuffItemGainAck del upkeep, asi que si lo metemos en el record queda
                    // solo en la semilla y el que entra tarde ve +1 por cada tick.
                }
                siteId++;
            }
        }
        public async void OnCapture(Player plr, uint _base, byte isCapture, uint distance)
        {
            var site = _sites[(int)_base - 1];
            if (isCapture == 1 && distance == 0)
            {
                if (site.Owner == plr.RoomInfo.Team.Team)
                    return;
                if (site.Capturer == null)
                {
                    site.Capturer = plr;
                    if (site.Owner == Team.Neutral)
                    {
                        for (uint pct = 3000; pct <= 30000; pct += 3000)
                        {
                            if (site.Capturer == null)
                                return;
                            if (plr.RoomInfo.Team == null)
                            {
                                site.Capturer = null;
                                var leftInfo = new List<SeizeUpdateInfoDto>
                                      {
                                          new SeizeUpdateInfoDto
                                          {
                                              Base = (ushort)_base,
                                              IsCaptured = 0,
                                              CurrentCaptureTeam = (byte)Team.Neutral,
                                              BaseOwner = (byte)Team.Neutral,
                                              Percentage = 0,
                                          }
                                      };
                                Room.Broadcast(new SeizeUpdateInfoAckMessage { Infos = leftInfo.ToArray() });
                                return;
                            }
                            _captureFlag = (uint)Team.Neutral;
                            _captureScore = 0;
                            _assistScore = 0;
                            site.PlayerWhoCapturedForUpKeep = null;
                            ulong[] assistIds = Array.Empty<ulong>();
                            if (!site.PlayerCloser.Contains(plr))
                                site.PlayerCloser.Add(plr);
                            if (pct == 30000)
                            {
                                site.Capturer = null;
                                site.PlayerWhoCapturedForUpKeep = plr;
                                _captureScore = 5;
                                _assistScore = 0;
                                _captureFlag = (uint)plr.RoomInfo.Team.Team;
                                site.Owner = plr.RoomInfo.Team.Team;
                                SpawnPickups((ushort)_base, 5);
                                GetRecord(plr).CapturePoints += 5;
                                plr.stats.GetSiegeStats().CaptureScore += 5;
                                plr.RoomInfo.Team.Score += 5;
                                assistIds = site.PlayerCloser.Where(p => p.RoomInfo.Team.Team == site.PlayerWhoCapturedForUpKeep.RoomInfo.Team.Team).
                                   Select(x => x.Account.Id).ToArray();
                            }
                            var updateInfo = new List<SeizeUpdateInfoDto>
                                  {
                                      new SeizeUpdateInfoDto
                                      {
                                          Base = (ushort)_base,
                                          IsCaptured = _captureFlag,
                                          CurrentCaptureTeam = (byte)plr.RoomInfo.Team.Team,
                                          BaseOwner = (byte)plr.RoomInfo.Team.Team,
                                          Percentage = (ushort)pct,
                                          PercentageGoal = 30000,
                                          // MEDIDO con sonda (1000/2000/3000/4000): cada campo
                                          // pinta un sitio distinto en el cliente ->
                                          //   CapturePoints -> contador "Capture Point"
                                          //   AssistPoints  -> contador "Capture Assist"
                                          //   Unk1          -> score del equipo + Total + Capture
                                          //   Points        -> contador de arriba a la derecha
                                          // Unk1 iba en 0: por eso el +5 no se veia en vivo.
                                          CapturePoints = _captureScore,
                                          AssistPoints = _assistScore,
                                          Unk1 = (int)_captureScore,
                                          Player = plr.Account.Id,
                                          Assists = assistIds,
                                          Points = (ushort)_captureScore
                                      }
                            };
                            if (_captureScore > 0)
                                System.Console.WriteLine($"[CAPTURA] base={_base} acc={plr.Account.Id} team={plr.RoomInfo.Team.Team} Unk1={_captureScore} -> teamScore={plr.RoomInfo.Team.Score}");
                            Room.Broadcast(new SeizeUpdateInfoAckMessage { Infos = updateInfo.ToArray() });
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                    else
                    {
                        for (int pct = -30000; pct <= 30000; pct += 3000)
                        {
                            if (site.Capturer == null)
                                return;
                            if (plr.RoomInfo.Team == null)
                            {
                                site.Capturer = null;
                                var leftInfo = new List<SeizeUpdateInfoDto>
                                      {
                                          new SeizeUpdateInfoDto
                                          {
                                              BaseOwner = (byte)site.Owner,
                                              Base = (ushort)_base,
                                              IsCaptured = (uint)site.Owner,
                                              CurrentCaptureTeam = (byte)Team.Neutral,
                                              Percentage = 30000,
                                          }
                                      };
                                Room.Broadcast(new SeizeUpdateInfoAckMessage { Infos = leftInfo.ToArray() });
                                return;
                            }
                            _captureFlag = (uint)Team.Neutral;
                            _captureScore = 0;
                            _assistScore = 0;
                            site.PlayerWhoCapturedForUpKeep = null;
                            ulong[] assistIds = Array.Empty<ulong>();
                            if (!site.PlayerCloser.Contains(plr))
                                site.PlayerCloser.Add(plr);
                            if (pct == 30000)
                            {
                                site.Capturer = null;
                                site.PlayerWhoCapturedForUpKeep = plr;
                                _captureScore = 5;
                                _assistScore = 0;
                                _captureFlag = (uint)plr.RoomInfo.Team.Team;
                                site.Owner = plr.RoomInfo.Team.Team;
                                SpawnPickups((ushort)_base, 5);
                                GetRecord(plr).CapturePoints += 5;
                                plr.stats.GetSiegeStats().CaptureScore += 5;
                                plr.RoomInfo.Team.Score += 5;
                                assistIds = site.PlayerCloser.Where(p => p.RoomInfo.Team.Team == site.PlayerWhoCapturedForUpKeep.RoomInfo.Team.Team).
                                   Select(x => x.Account.Id).ToArray();
                            }
                            var updateInfo = new List<SeizeUpdateInfoDto>
                                  {
                                      new SeizeUpdateInfoDto
                                      {
                                          Base = (ushort)_base,
                                          IsCaptured = _captureFlag,
                                          CurrentCaptureTeam = pct < 0 ? (byte)site.Owner : (byte) plr.RoomInfo.Team.Team,
                                          BaseOwner = pct < 0 ? (byte)site.Owner : (byte) plr.RoomInfo.Team.Team,
                                          Percentage = (ushort)pct,
                                          PercentageGoal = 30000,
                                          // MEDIDO con sonda (1000/2000/3000/4000): cada campo
                                          // pinta un sitio distinto en el cliente ->
                                          //   CapturePoints -> contador "Capture Point"
                                          //   AssistPoints  -> contador "Capture Assist"
                                          //   Unk1          -> score del equipo + Total + Capture
                                          //   Points        -> contador de arriba a la derecha
                                          // Unk1 iba en 0: por eso el +5 no se veia en vivo.
                                          CapturePoints = _captureScore,
                                          AssistPoints = _assistScore,
                                          Unk1 = (int)_captureScore,
                                          Player = plr.Account.Id,
                                          Assists = assistIds,
                                          Points = (ushort)_captureScore
                                      }
                            };
                            if (_captureScore > 0)
                                System.Console.WriteLine($"[CAPTURA] base={_base} acc={plr.Account.Id} team={plr.RoomInfo.Team.Team} Unk1={_captureScore} -> teamScore={plr.RoomInfo.Team.Score}");
                            Room.Broadcast(new SeizeUpdateInfoAckMessage { Infos = updateInfo.ToArray() });
                            await Task.Delay(TimeSpan.FromSeconds(2));
                        }
                    }
                }
            }
            else if (isCapture == 0 && distance == 0)
            {
                if (site.Capturer == plr)
                {
                    site.PlayerCloser.RemoveAll(p => p == site.Capturer);
                    site.Capturer = null;
                    _captureScore = 0;
                    _assistScore = 0;
                    site.PlayerWhoCapturedForUpKeep = null;
                    var releaseInfo = new List<SeizeUpdateInfoDto>
                          {
                              new SeizeUpdateInfoDto
                              {
                                  Base = (ushort)_base,
                                  IsCaptured = site.Owner == Team.Neutral ? 3 : (uint)site.Owner,
                                  CurrentCaptureTeam = (byte)Team.Neutral,
                                  BaseOwner = (byte)site.Owner
                              }
                          };
                    Room.Broadcast(new SeizeUpdateInfoAckMessage { Infos = releaseInfo.ToArray() });
                    return;
                }
            }
        }
        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            return new SiegePlayerRecord(plr);
        }
        private static SiegePlayerRecord GetRecord(Player plr)
        {
            return (SiegePlayerRecord)plr.RoomInfo.Stats;
        }
        public override void OnScoreKill(Player killer, Player assist, Player target, AttackAttribute attackAttribute,
            LongPeerId scoreTarget, LongPeerId scoreKiller, LongPeerId scoreAssist)
        {
            base.OnScoreKill(killer, assist, target, attackAttribute, scoreTarget, scoreKiller, scoreAssist);
            if (!ScoreIsPlaying())
                return;
            if (scoreTarget.PeerId.Category == PlayerCategory.Player)
            {
                if (target?.RoomInfo?.Team != null && killer?.RoomInfo?.Team != null)
                {
                    killer.stats.GetSiegeStats().BattleScore += 2;
                    if (assist != null)
                        assist.stats.GetSiegeStats().BattleScore++;
                }
            }
        }
        public override void OnScoreSuicide(Player target, LongPeerId scoreTarget, AttackAttribute icon)
        {
            base.OnScoreSuicide(target, scoreTarget, icon);
            if (!ScoreIsPlaying())
                return;
            if (scoreTarget.PeerId.Category == PlayerCategory.Player)
            {
                var targetTeam = target?.RoomInfo?.Team;
                if (targetTeam != null)
                {
                    GetRecord(target).Suicides++;
                }
            }
        }
        private bool CanStartGame()
        {
            if (!StateMachine.IsInState(GameRuleState.Waiting))
                return false;
            if (Room.Options.IsFriendly)
                return true;
            var teams = Room.TeamManager.Values.ToArray();
            if (teams.Any(team => team.Count == 0))
                return false;
            return teams.All(team => team.Players.Any(plr => plr.RoomInfo.IsReady || Room.Master == plr));
        }
    }
    internal class SiegeBriefing : Briefing
    {
        public SiegeBriefing(GameRuleBase ruleBase)
            : base(ruleBase)
        {
        }
    }
    internal class SiegePlayerRecord : PlayerRecord
    {
        public SiegePlayerRecord(Player plr)
            : base(plr)
        {
        }
        public override uint TotalScore => CapturePoints + CaptureAssists + Heal + Kills * 2 + KillAssists;
        public uint ObtainedItems { get; set; }
        public uint CapturePoints { get; set; }
        public uint CaptureAssists { get; set; }
        public uint Heal { get; set; }
        public override void Serialize(BinaryWriter w, bool isResult)
        {
            base.Serialize(w, isResult);
            w.Write(Kills);
            w.Write(KillAssists);
            w.Write(Heal);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            // MEDIDO: este 4to relleno es la columna Battle (sonda 777 aparecio ahi).
            w.Write((int)(Kills * 2 + KillAssists));
            // Estas tres las SUMA el cliente y ademas las arma con los SeizeUpdateInfoAck en
            // vivo, asi que a mitad de partida van en 0 o se duplican. Al que entra tarde se le
            // reproducen esos acks en OnIntrudeCompleted, no por aca.
            // MEDIDO: la columna Capture SUMA estos tres (sonda 888+999+1111 = 2998 en
            // pantalla). Por eso las fichas y las asistencias inflaban Capture. Solo el
            // primero lleva el valor; los otros dos tienen sus contadores aparte, que el
            // cliente arma con los acks en vivo.
            w.Write(CapturePoints);
            w.Write(0);
            w.Write(0);
        }
        public override void Reset()
        {
            base.Reset();
            Heal = 0;
            CapturePoints = 0;
            ObtainedItems = 0;
        }
        public override int GetExpGain(out int bonusExp)
        {
            base.GetExpGain(out bonusExp);
            var config = Config.Instance.Game.SiegeExpRates;
            var rank = 1;
            var ranked = Player.Room.TeamManager.Players
                .Where(plr => plr.RoomInfo.State == PlayerState.Waiting &&
                              plr.RoomInfo.Mode == PlayerGameMode.Normal)
                .ToArray();
            foreach (var plr in ranked.OrderByDescending(plr => plr.RoomInfo.Stats.TotalScore))
            {
                if (plr == Player)
                    break;
                rank++;
                if (rank > 3)
                    break;
            }
            var placementBonus = 0f;
            switch (rank)
            {
                case 1:
                    placementBonus = config.FirstPlaceBonus;
                    break;
                case 2:
                    placementBonus = config.SecondPlaceBonus;
                    break;
                case 3:
                    placementBonus = config.ThirdPlaceBonus;
                    break;
            }
            return (int)(TotalScore * config.ScoreFactor +
                          placementBonus +
                          ranked.Length * config.PlayerCountFactor +
                          Player.RoomInfo.PlayTime.TotalMinutes * config.ExpPerMin);
        }
    }
}

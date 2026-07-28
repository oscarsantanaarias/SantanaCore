namespace Santana.Network.Services
{
    using SantanaLib.DotNetty.Handlers.MessageHandling;
    using Dapper.FastCrud;
    using Santana.Database.Game;
    using Santana.Network.Message.Game;
    using ProudNetSrc.Handlers;
    using Serilog;
    using Serilog.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class UnionService : ProudMessageHandler
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(UnionService));

        public static void SLogOn(Player plr, bool noRooms = false)
        {
            if (plr?.Club == null)
                return;

            try
            {
                var peers = CollectOnlineMembers(plr).ToArray();
                if (peers.Length == 0)
                    return;

                foreach (var peer in peers)
                {
                    _ = plr.SendAsync(new ClubUnionSystemUserInfoAckMessage
                    {
                        Data = MakeUserInfo(peer)
                    });

                    if (!ReferenceEquals(peer, plr))
                    {
                        _ = peer.SendAsync(new ClubUnionSystemUserInfoAckMessage
                        {
                            Data = MakeUserInfo(plr)
                        });
                    }
                }

                _log.Information(
                    "Union presence broadcast finished for {player} of club {clubId}; {count} associates notified, {noRooms} outside any room",
                    plr.Account.Nickname,
                    plr.Club.Id,
                    peers.Length,
                    noRooms);
            }
            catch (Exception err)
            {
                _log.Error(err, "Union presence broadcast threw partway through for {player}", plr?.Account?.Nickname);
            }
        }

        [MessageHandler(typeof(UnionMainUiReqMessage))]
        public async Task UnionMainUiReq(GameSession session, UnionMainUiReqMessage message)
        {
            var me = session?.Player;
            if (me == null)
                return;

            _log.Information(
                "[UNION MAIN UI REQ] player={player} club={clubId}",
                me.Account.Nickname,
                me.Club?.Id ?? 0);

            var peers = CollectOnlineMembers(me).ToArray();

            await session.SendAsync(new UnionMainUiAckMessage
            {
                Data = MakeMainUi(me),
                Unk1 = 0
            });

            foreach (var peer in peers)
            {
                await session.SendAsync(new ClubUnionSystemUserInfoAckMessage
                {
                    Data = MakeUserInfo(peer)
                });
            }
        }

        [MessageHandler(typeof(UnionSearchRoomReqMessage))]
        public async Task UnionSearchRoomReq(GameSession session, UnionSearchRoomReqMessage message)
        {
            var me = session?.Player;
            if (me == null)
                return;

            var hit = CollectOnlineMembers(me)
                .Where(p => p.Room != null && p.Channel != null && p.Account.Id != me.Account.Id)
                .OrderBy(p => p.Channel.Id)
                .ThenBy(p => p.Room.Id)
                .ThenBy(p => p.Account.Id)
                .FirstOrDefault();

            _log.Information(
                "[UNION SEARCH ROOM] player={player} unk={unk} found={found} channel={channel} room={room}",
                me.Account.Nickname,
                message?.Unk1 ?? 0,
                hit != null,
                hit?.Channel?.Id ?? 0,
                hit?.Room?.Id ?? 0);

            await session.SendAsync(new UnionSearchRoomAckMessage
            {
                Unk1 = (int)(hit?.Channel?.Id ?? 0),
                Unk2 = (int)(hit?.Room?.Id ?? 0)
            });
        }

        private static ClubUnionUserInfo MakeUserInfo(Player plr)
        {
            return new ClubUnionUserInfo
            {
                Unk1 = plr?.Account?.Id ?? 0,
                Unk2 = (int)(plr?.Club?.Id ?? 0),
                Unk3 = (byte)(plr?.Channel != null ? 1 : 0)
            };
        }

        private static UnionMainUiDto MakeMainUi(Player plr)
        {
            var ui = new UnionMainUiDto();

            if (plr?.Club == null)
                return ui;

            var clubs = CollectUnionClubs(plr).ToArray();
            var members = CollectOnlineMembers(plr).ToArray();
            var partnerClub = clubs.FirstOrDefault(c => c.Id != plr.Club.Id);

            ui.Unk1 = clubs
                .Select(c => new UnionMainUiListEntryDto
                {
                    Unk1 = c.ClanName ?? "",
                    Unk2 = (short)Math.Clamp((int)c.ClanRank, short.MinValue, short.MaxValue),
                    Unk3 = (int)c.ClubPoints
                })
                .ToArray();

            ui.Unk2 = members
                .Select(m => new UnionMainUiListEntryDto
                {
                    Unk1 = m.Account?.Nickname ?? "",
                    Unk2 = m.Level,
                    Unk3 = (int)(m.Room?.Id ?? 0)
                })
                .ToArray();

            ui.Unk3 = clubs
                .Select(c => new UnionMainUiListEntryDto
                {
                    Unk1 = c.ClanName ?? "",
                    Unk2 = (short)Math.Clamp((int)c.ClubWin, short.MinValue, short.MaxValue),
                    Unk3 = (int)c.ClubLoss
                })
                .ToArray();

            ui.Unk4 = Array.Empty<UnionMainUiListEntryDto>();
            ui.Unk5 = Array.Empty<UnionMainUiListEntryDto>();
            ui.Unk6 = Array.Empty<UnionMainUiListEntryDto>();
            ui.Unk7 = Array.Empty<UnionMainUiListEntryDto>();

            ui.Unk8.Unk1 = (byte)Math.Clamp(clubs.Length, 0, byte.MaxValue);
            ui.Unk8.Unk2 = (byte)Math.Clamp(members.Length, 0, byte.MaxValue);
            ui.Unk8.Unk3 = (byte)(plr.Room != null ? 1 : 0);
            ui.Unk8.Unk4 = 0;
            ui.Unk8.Unk5 = 0;
            ui.Unk8.Unk6 = 0;
            ui.Unk8.Unk7 = 0;
            ui.Unk8.Unk8 = plr.Club.ClanName ?? "";
            ui.Unk8.Unk9 = partnerClub?.ClanName ?? "";
            ui.Unk8.Unk10 = (int)plr.Club.ClubPoints;
            ui.Unk8.Unk11 = (int)(partnerClub?.ClubPoints ?? 0);
            ui.Unk8.Unk12 = clubs.Sum(c => (int)c.ClubPoints);

            var mine = MakeStatRow(plr.Club);
            var theirs = MakeStatRow(partnerClub);

            ui.Unk8.Unk13 = mine;
            ui.Unk8.Unk14 = theirs;
            ui.Unk8.Unk15 = mine;
            ui.Unk8.Unk16 = theirs;
            ui.Unk8.Unk17 = mine;
            ui.Unk8.Unk18 = theirs;
            ui.Unk8.Unk19 = mine;
            ui.Unk8.Unk20 = theirs;
            ui.Unk8.Unk21 = mine;
            ui.Unk8.Unk22 = theirs;
            ui.Unk8.Unk23 = mine;
            ui.Unk8.Unk24 = theirs;
            ui.Unk8.Unk25 = mine;
            ui.Unk8.Unk26 = theirs;

            return ui;
        }

        private static UnionMainUiStatRowDto MakeStatRow(Club club)
        {
            if (club == null)
                return new UnionMainUiStatRowDto();

            return new UnionMainUiStatRowDto
            {
                Unk1 = club.Level,
                Unk2 = (int)club.ClubPoints,
                Unk3 = (int)club.ClanRank,
                Unk4 = (int)club.ClubWin,
                Unk5 = (int)club.ClubLoss,
                Unk6 = (int)club.Id,
                Unk7 = club.Count,
                Unk8 = 0,
                Unk9 = 0
            };
        }

        private static IEnumerable<Player> CollectOnlineMembers(Player plr)
        {
            var ids = ResolveUnionClubIds(plr);
            return GameServer.Instance.PlayerManager
                .Where(p => p?.Account != null && p.Club != null && ids.Contains(p.Club.Id))
                .GroupBy(p => p.Account.Id)
                .Select(g => g.First());
        }

        private static IEnumerable<Club> CollectUnionClubs(Player plr)
        {
            var ids = ResolveUnionClubIds(plr);
            return GameServer.Instance.ClubManager
                .Where(c => c != null && ids.Contains(c.Id))
                .GroupBy(c => c.Id)
                .Select(g => g.First());
        }

        private static HashSet<uint> ResolveUnionClubIds(Player plr)
        {
            var ids = new HashSet<uint>();
            if (plr?.Club == null)
                return ids;

            using (var db = GameDatabase.Open())
            {
                var edges = DbUtil.Find<ClanUnionDto>(db).ToArray();
                ids.Add(plr.Club.Id);

                var expanded = true;
                while (expanded)
                {
                    expanded = false;

                    foreach (var edge in edges)
                    {
                        if (!ids.Contains(edge.ClubId) && !ids.Contains(edge.UnionId))
                            continue;

                        if (ids.Add(edge.ClubId))
                            expanded = true;

                        if (ids.Add(edge.UnionId))
                            expanded = true;
                    }
                }
            }

            return ids;
        }
    }
}

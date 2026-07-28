using Dapper.FastCrud;
using Santana.Database.Game;
using Santana.Network.Message.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Santana.Network.Services
{
    internal static class ClanWarService
    {
        public static bool IsStarted = false;
        public static bool RegisterOpen = false;
        public static long StartedTime = 0;
        public static long WaitingTime = 0;

        public class ClanWarClans
        {
            public string ClanBName { get; set; }
            public int PlayersCount { get; set; }
            public int Points { get; set; }
            public long CreatedTime { get; set; }
        }

        public static void RemoveClub()
        {
            using (var conn = GameDatabase.Open())
            {
                var status = "Lobby";

                var lobbyEvent = DbUtil.Find<ClanWarEventDto>(conn, statement => statement
                       .Where($"{nameof(ClanWarEventDto.Status):C} = @{nameof(status)}")
                       .WithParameters(new { status })).FirstOrDefault();

                if (lobbyEvent == null)
                    return;

                if ((WaitingTime - DateTimeOffset.Now.ToUnixTimeSeconds()) <= 0)
                    DbUtil.Delete(conn, lobbyEvent);
            }
        }

        public static void StartMixClans()
        {
            using (var conn = GameDatabase.Open())
            {
                var entries = DbUtil.Find<ClanWarsDto>(conn).ToList();

                if (entries.Count % 2 != 0)
                {
                    DbUtil.Delete(conn, entries[entries.Count - 1]);
                    entries.RemoveAt(entries.Count - 1);
                }

                var shuffledNames = new List<string>();
                var firstHalf = new List<string>();
                var secondHalf = new List<string>();

                foreach (var entry in entries.ShuffleSecure())
                {
                    shuffledNames.Add(entry.Clan);
                    DbUtil.Delete(conn, entry);
                }

                var half = entries.Count / 2;

                for (int i = 0; i < half; i++)
                    firstHalf.Add(shuffledNames[i]);

                for (int i = half; i < entries.Count; i++)
                    secondHalf.Add(shuffledNames[i]);

                for (var i = firstHalf.Count - 1; i >= 0; i--)
                {
                    var pairing = new ClanWarEventDto
                    {
                        ClanAName = firstHalf[i],
                        ClanBName = secondHalf[i],
                        Status = "Lobby",
                        Winner = ""
                    };
                    DbUtil.Insert(conn, pairing);
                }

                entries.Clear();
            }
        }

        public static void StartClanWar()
        {
            var weekday = DateTime.Now.ToString("dddd");
            var now = DateTime.Now;

            using (var conn = GameDatabase.Open())
            {
                var schedule = DbUtil.Find<ClanWarDto>(conn, statement => statement
                   .Where($"{nameof(ClanWarDto.Days):C} = @{nameof(weekday)}")
                    .WithParameters(new { Day = weekday })).FirstOrDefault();

                if (schedule == null)
                    return;

                var scheduledHour = schedule.StartTime.Substring(0, schedule.StartTime.IndexOf(':'));

                if (schedule.Days != weekday || scheduledHour != now.Hour.ToString())
                    return;

                if (schedule.StartTime == schedule.EndTime)
                {
                    GameServer.Instance.BroadcastNotice("Clan War register is closed.");
                    RegisterOpen = false;
                    StartMixClans();
                    WaitingTime = DateTimeOffset.Now.Add(TimeSpan.FromMinutes(15)).ToUnixTimeSeconds();
                }
                else if (!IsStarted)
                {
                    RegisterOpen = true;
                    GameServer.Instance.BroadcastNotice("Clan War register is opening.");
                    IsStarted = true;
                }
            }
        }

        public static void SetClubPoints(Player plr, uint points)
        {
            using (var conn = GameDatabase.Open())
            {
                var club = DbUtil.Find<ClubDto>(conn, statement => statement
                       .Where($"{nameof(ClubDto.Id):C} = @Id")
                       .WithParameters(new { plr.Club.Id })).FirstOrDefault();

                club.Points += points;
                DbUtil.Update(conn, club);
            }
        }

        public static bool CheckPlayer(Player plr)
        {
            using (var conn = GameDatabase.Open())
            {
                var isParticipating = DbUtil.Find<ClanWarEventDto>(conn, statement => statement
                    .Where($"{nameof(ClanWarEventDto.ClanAName):C} = @{nameof(plr.Club.ClanName)} OR {nameof(ClanWarEventDto.ClanBName):C} = @{nameof(plr.Club.ClanName)}")
                    .WithParameters(new { plr.Club.ClanName })).Any();

                return isParticipating;
            }
        }

        public static void AddWinnerClan(Player plr)
        {
            using (var conn = GameDatabase.Open())
            {
                var alreadyRegistered = DbUtil.Find<ClanWarsDto>(conn, statement => statement
                    .Where($"{nameof(ClanWarsDto.Clan):C} = @{nameof(plr.Club.ClanName)}")
                    .WithParameters(new { plr.Club.ClanName })).Any();

                if (alreadyRegistered)
                    return;

                var advancing = new ClanWarsDto
                {
                    Clan = plr.Club.ClanName
                };
                DbUtil.Insert(conn, advancing);
            }

            SetWinnerTeam(plr);
        }

        public static void SetWinnerTeam(Player plr)
        {
            using (var conn = GameDatabase.Open())
            {
                var survivors = DbUtil.Find<ClanWarsDto>(conn).ToList();
                var openEvents = DbUtil.Find<ClanWarEventDto>(conn).ToList();

                if (openEvents.Count == 0 && survivors.Count > 1)
                {
                    StartMixClans();
                }
                else if (openEvents.Count == 0 && survivors.Count == 1)
                {
                    GameServer.Instance.BroadcastNotice($"Clan War register is end winner is {plr.Club.ClanName}.");
                    SetClubPoints(plr, 10);

                    foreach (var member in plr.Room.Players.Where(x => x.Value.Club.ClanName == plr.Club.ClanName))
                    {

                    }
                }
            }
        }
    }
}

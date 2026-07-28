using Dapper.FastCrud;
using DotNetty.Transport.Channels;
using Santana.Database.Auth;
using Santana.Network;
using Santana.Network.Services;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Santana
{
    public class PacketsInfo
    {
        public object message { get; set; }
        public IChannelHandlerContext context { get; set; }
    }

    internal static class PacketFilter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "Packet Filter");
        public static List<Tuple<PacketsInfo, DateTimeOffset>> packetFillters = new List<Tuple<PacketsInfo, DateTimeOffset>>();

        public static void GetPacketFillters(IChannelHandlerContext context, object message)
        {
            try
            {
                var IP = context.Channel.RemoteAddress.ToString().Replace("[::ffff:", "").Split(']')[0];

                var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);

                packetFillters = packetFillters.Where(entry => entry != null && entry.Item2 > cutoff).ToList();

                var sameContext = packetFillters.Where(entry => entry.Item1.context == context).ToList();
                var repeatCount = sameContext?.Count(entry => entry.Item1.message == message);
                if (repeatCount > 15)
                {
                    Logger?.Warning($"Flood threshold exceeded - blacklisting address {IP} for repeated identical traffic");

                    context.Channel.CloseAsync();

                    using (var db = AuthDatabase.Open())
                    {
                        var lastLogin = db.Find<LoginHistoryDto>(statement => statement
                            .Where($"{nameof(LoginHistoryDto.IP):C} = @{nameof(IP)}")
                            .WithParameters(new { IP })).Last();
                        var offenderId = lastLogin.AccountId;

                        var spamLog = "";
                        foreach (var entry in sameContext)
                            spamLog += $", {entry.Item1.message}";

                        if (!lastLogin.Account.Bans.Any(existing => existing.AccountId == lastLogin.AccountId))
                        {
                            var forever = TimeSpan.FromHours(9999999);

                            BanDto ban = new BanDto
                            {
                                AccountId = offenderId,
                                Date = 0,
                                Duration = DateTimeOffset.Now.Add(forever).ToUnixTimeSeconds(),
                                Reason = "Packet Spam",
                                Log = spamLog
                            };
                            db.Insert(ban);
                        }
                    }
                }

                packetFillters.Add(new Tuple<PacketsInfo, DateTimeOffset>(new PacketsInfo() { message = message, context = context }, DateTimeOffset.UtcNow));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    internal static class RoomFilter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "Room Filter");
        private static List<Tuple<ulong, DateTimeOffset>> recentRoomHits = new List<Tuple<ulong, DateTimeOffset>>();

        public static int GetRoomFillter(ulong PlayerId)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(30);
            if (recentRoomHits != null)
            {
                recentRoomHits = recentRoomHits.Where(entry => entry?.Item2 > cutoff).ToList();
                var hitsForPlayer = recentRoomHits.Count(entry => entry?.Item1 == PlayerId);

                if (hitsForPlayer > 2)
                {
                    return 1;
                }
            }
            recentRoomHits.Add(new Tuple<ulong, DateTimeOffset>(PlayerId, DateTimeOffset.UtcNow));

            return 0;
        }
    }

    internal static class PlayerPingFilter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "Ping Filter");
        public static List<Tuple<ulong, DateTimeOffset>> PlayerPingFillters = new List<Tuple<ulong, DateTimeOffset>>();

        public static int GetPlayerPingFillter(ulong PlayerId)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromMinutes(1);

            PlayerPingFillters = PlayerPingFillters.Where(entry => entry?.Item2 > cutoff).ToList();
            var hitsForPlayer = PlayerPingFillters.Count(entry => entry?.Item1 == PlayerId);

            PlayerPingFillters.Add(new Tuple<ulong, DateTimeOffset>(PlayerId, DateTimeOffset.UtcNow));

            return hitsForPlayer;
        }
    }

    internal static class TimeFilter
    {
        private static readonly ILogger Logger = Log.ForContext("SourceContext", "Room Filter");
        private static List<Tuple<GameSession, DateTimeOffset>> recentSessionHits = new List<Tuple<GameSession, DateTimeOffset>>();

        public static int GetRoomFillter(GameSession session)
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromSeconds(1);
            recentSessionHits = recentSessionHits.Where(entry => entry.Item2 > cutoff).ToList();

            var hitsForSession = recentSessionHits.Count(entry => entry.Item1 == session);

            if (hitsForSession >= 1)
                return 1;

            recentSessionHits.Add(new Tuple<GameSession, DateTimeOffset>(session, DateTimeOffset.UtcNow));

            return 0;
        }
    }
}

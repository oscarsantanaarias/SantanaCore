using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Network;
using Serilog.Core;

namespace Santana.Commands
{
    internal class UnbanCommands : ICommand
    {
        public UnbanCommands()
        {
            Name = "/unban";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[0];
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > unban mismo <nickname>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage mismo, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /unban <username>");
                return true;
            }

            var name = args[0];
            using (var handle = AuthDatabase.Open())
            {
                var record = (await DbUtil.FindAsync<AccountDto>(handle, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = name }))
                    ).FirstOrDefault();

                if (record == null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                    return true;
                }

                foreach (var entry in record.Bans)
                {
                    entry.Duration = 0;
                    await DbUtil.UpdateAsync(handle, entry);
                }

                await DbUtil.UpdateAsync(handle, record);
                plr?.Channel?.SendMessage(plr, "system", "Unbanned " + record.Nickname, NewChatType.Whisper);
                plr?.SendConsoleMessage(S4Color.Green + $"Unbanned {record.Nickname}");
            }

            return true;
        }

        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("");
                sb.AppendLine(cmd.Help());
            }

            return sb.ToString();
        }
    }

    internal class BanCommands : ICommand
    {
        public BanCommands()
        {
            Name = "/ban";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[0];
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 1)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: >", NewChatType.Whisper);

                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> roomkick - roomkick");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> - permanent ban");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> pardon - unban");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> days <duration(days)> - ban for x days");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> mins <duration(minutes)> - ban for x minutes");
                plr.SendConsoleMessage(S4Color.Red + "> /ban <username> secs <duration(seconds)> - ban for x seconds");
                plr.SendConsoleMessage(S4Color.Red +
                                       "> /ban <username> <currentdate(ex:20180130)> <unk> <duration(seconds)>");
                return true;
            }

            if (args.Length < 2)
            {
                Array.Resize(ref args, args.Length + 1);
                args[1] = "none";
            }

            var lift = false;
            var name = args[0];
            var seconds = 0;

            try
            {
                AccountDto record;
                switch (args[1])
                {
                    case "pardon":
                        lift = true;
                        break;
                    case "roomkick":
                        using (var handle = AuthDatabase.Open())
                        {
                            record = (await DbUtil.FindAsync<AccountDto>(handle, statement => statement
                                    .Include<BanDto>(join => join.LeftOuterJoin())
                                    .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                                    .WithParameters(new { Nickname = name }))
                                ).FirstOrDefault();

                            if (record == null)
                            {
                                plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                                plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                                return true;
                            }

                            var victim = GameServer.Instance.PlayerManager.Get((ulong)record.Id);
                            if (victim == null)
                            {
                                plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                                plr.SendConsoleMessage(S4Color.Red + "Player is not online");
                                return true;
                            }

                            victim.Room?.Leave(victim, RoomLeaveReason.ModeratorKick);
                            plr?.Channel?.SendMessage(plr, "system", "Kicked " + record.Nickname + "out of room", NewChatType.Whisper);
                            plr.SendConsoleMessage(S4Color.Green + $"Kicked {record.Nickname} out of room");
                        }

                        return true;
                    case "secs":
                        int.TryParse(args[2], out seconds);
                        break;
                    case "mins":
                        int.TryParse(args[2], out var mins);
                        seconds = mins * 60;
                        break;
                    case "days":
                        int.TryParse(args[2], out var days);
                        seconds = days * 24 * 60 * 60;
                        break;
                    default:
                        if (DateTimeOffset.TryParseExact(args[1], "yyyyMMdd", CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out _))
                        {
                            if (args.Length >= 4)
                            {
                                int.TryParse(args[2], out var flag);
                                int.TryParse(args[3], out var millis);
                                seconds = millis / 60;
                            }
                        }
                        else
                        {
                            seconds = (int)TimeSpan.FromDays(10 * 365).TotalSeconds;
                        }

                        break;
                }

                using (var handle = AuthDatabase.Open())
                {
                    record = (await DbUtil.FindAsync<AccountDto>(handle, statement => statement
                            .Include<BanDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                            .WithParameters(new { Nickname = name }))
                        ).FirstOrDefault();

                    if (record == null)
                    {
                        plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                        return true;
                    }

                    if (lift)
                    {
                        foreach (var entry in record.Bans)
                        {
                            entry.Duration = 0;
                            await DbUtil.UpdateAsync(handle, entry);
                        }

                        await DbUtil.UpdateAsync(handle, record);
                        plr?.Channel?.SendMessage(plr, "system", "Unbanned " + record.Nickname, NewChatType.Whisper);
                        plr?.SendConsoleMessage(S4Color.Green + $"Unbanned {record.Nickname}");
                    }
                    else
                    {
                        if (plr.Account.SecurityLevel <= (SecurityLevel)record.SecurityLevel)
                        {
                            plr?.Channel?.SendMessage(plr, "system", "You cannot ban this player", NewChatType.Whisper);
                            plr.SendConsoleMessage($"You cannot ban this player");
                            return true;
                        }

                        var span = TimeSpan.FromSeconds(seconds);

                        var banRow = new BanDto
                        {
                            AccountId = record.Id,
                            Account = record,
                            Date = 0,
                            Duration = DateTimeOffset.Now.Add(span).ToUnixTimeSeconds(),
                            Reason = $"GMConsole - {plr?.Account?.Nickname ?? "n/A"}"
                        };

                        var victim = GameServer.Instance.PlayerManager.Get((ulong)record.Id);
                        victim?.Session?.CloseAsync();

                        await DbUtil.InsertAsync(handle, banRow);
                        await DbUtil.UpdateAsync(handle, record);

                        var readable = new StringBuilder();
                        if (span.Days > 0)
                            readable.AppendFormat("{0} days ", span.Days);
                        if (span.Hours > 0)
                            readable.AppendFormat("{0} hours ", span.Hours);
                        if (span.Minutes > 0)
                            readable.AppendFormat("{0} minutes ", span.Minutes);
                        if (span.Seconds > 0)
                            readable.AppendFormat("{0} seconds ", span.Seconds);
                        plr?.Channel?.SendMessage(plr, "system", "Banned " + record.Nickname + " for " + readable, NewChatType.Whisper);
                        plr?.SendConsoleMessage(S4Color.Green + $"Banned {record.Nickname} for {readable}");
                    }
                }
            }
            catch (Exception)
            {
                plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Unknown player");
            }

            return true;
        }

        public string Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Name);
            foreach (var cmd in SubCommands)
            {
                sb.Append("");
                sb.AppendLine(cmd.Help());
            }

            return sb.ToString();
        }
    }
}

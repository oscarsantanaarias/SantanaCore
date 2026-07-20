using Santana.Network.Message.Chat;
namespace Santana.Commands
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Dapper.FastCrud;
    using ExpressMapper.Extensions;
    using Santana.Database.Auth;
    using Santana.Database.Game;
    using Santana.Network;
    using Santana.Network.Data.Club;
    using Santana.Network.Message.Club;
    internal class ClanCommands : ICommand
    {
        public ClanCommands()
        {
            Name = "/clan";
            AllowConsole = true;
            Permission = SecurityLevel.User;
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
                if (plr.Account.SecurityLevel >= SecurityLevel.GameMaster)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> /clan forcejoin <username> <clan>");
                    plr.SendConsoleMessage(S4Color.Red + "> /clan forcekick <username> <clan>");
                    plr.SendConsoleMessage(S4Color.Red + "> /clan forcemaster <username> <clan>");
                    plr.SendConsoleMessage(S4Color.Red + "> /clan invite <username>");
                    plr.SendConsoleMessage(S4Color.Red + "> /clan kick <username>");
                }
                else
                {
                    plr?.SendAsync(new MessageChatAckMessage(
                        ChatType.Channel,
                        plr.Account.Id,
                        "ClanMgr",
                        "/clan invite <username>"));
                    plr?.SendAsync(new MessageChatAckMessage(
                        ChatType.Channel,
                        plr.Account.Id,
                        "ClanMgr",
                        "/clan kick <username>"));
                }
                return true;
            }
            var memberScope = args[0].ToLower() == "kick" && args[0].ToLower() == "invite";
            if (args.Length < 2)
            {
                Array.Resize(ref args, 3);
                args[1] = "none";
                args[2] = "none";
            }
            var targetNick = args[1].ToLower();
            var tailArgs = new string[args.Length - 2];
            Array.Copy(args, 2, tailArgs, 0, tailArgs.Length);
            var clanNameBuilder = new StringBuilder();
            foreach (var piece in tailArgs)
            {
                clanNameBuilder.Append(" " + piece);
            }
            var clanQuery = clanNameBuilder.ToString().Trim().ToLower();
            Club targetClub;
            if (plr?.Account.SecurityLevel >= SecurityLevel.GameMaster && !memberScope)
            {
                targetClub = GameServer.Instance.ClubManager.FirstOrDefault(x => x.ClanName.ToLower() == clanQuery);
                if (targetClub == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Unknown clan " + clanQuery);
                    return true;
                }
            }
            else
            {
                targetClub = plr?.Club;
                if (targetClub == null)
                {
                    plr?.SendAsync(new MessageChatAckMessage(
                        ChatType.Channel,
                        plr.Account.Id,
                        "ClanMgr",
                        "You are not inside a clan"));
                    return true;
                }
            }
            var onlinePlayer = GameServer.Instance.PlayerManager
                .FirstOrDefault(x => x.Account?.Nickname?.ToLower() == targetNick);
            AccountDto accountRow;
            using (var db = AuthDatabase.Open())
            {
                accountRow = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = targetNick }))).FirstOrDefault();
                if (accountRow == null)
                {
                    if (onlinePlayer == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                        return true;
                    }
                    accountRow = onlinePlayer.Account.AccountDto;
                }
            }
            switch (args[0].ToLower())
            {
                case "kick":
                    {
                        var reply = "You cannot kick a player";
                        var callerRank = plr.Club.GetPlayer(plr.Account.Id).Rank;
                        if (plr.Account.Id == (ulong)accountRow.Id)
                        {
                            reply = "You cannot kick yourself";
                        }
                        else if (callerRank <= ClubRank.Staff)
                        {
                            if (targetClub.GetPlayer((ulong)accountRow.Id).Rank < callerRank)
                            {
                                reply = "You cannot kick a player with a higher rank";
                            }
                            else
                            {
                                if (onlinePlayer != null)
                                    Club.LogOff(onlinePlayer);
                                await targetClub.RemovePlayer((ulong)accountRow.Id);
                                reply = "Kicked player from clan";
                            }
                        }
                        await plr.SendAsync(new MessageChatAckMessage(
                            ChatType.Channel,
                            plr.Account.Id,
                            "ClanMgr",
                            reply));
                        return true;
                    }
                case "invite":
                    {
                        var reply = "You cannot invite a player";
                        if (plr.Club.GetPlayer(plr.Account.Id).Rank <= ClubRank.Staff)
                        {
                            if (onlinePlayer != null)
                            {
                                if (targetClub.Players.ContainsKey(onlinePlayer.Account.Id))
                                {
                                    reply = "Player is already in your clan";
                                }
                                else
                                {
                                    plr.Club.SendInvite(plr, onlinePlayer);
                                    reply = "Player has been invited";
                                }
                            }
                            else
                            {
                                reply = "Player is not online";
                            }
                        }
                        await plr.SendAsync(new MessageChatAckMessage(
                            ChatType.Channel,
                            plr.Account.Id,
                            "ClanMgr",
                            reply));
                        return true;
                    }
                case "forcejoin":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                            goto default;
                        if (GameServer.Instance.ClubManager.Any(x => x.Players.ContainsKey((ulong)accountRow.Id)))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is already in a clan");
                            return true;
                        }
                        if (await targetClub.AddPlayer((ulong)accountRow.Id))
                        {
                            plr?.SendConsoleMessage(S4Color.Green +
                                                    $"Added player {accountRow.Nickname} to clan {targetClub.ClanName}");
                        }
                        return true;
                    }
                case "forcekick":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                            goto default;
                        if (!GameServer.Instance.ClubManager.Any(x => x.Players.ContainsKey((ulong)accountRow.Id)))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is not in a clan");
                            return true;
                        }
                        if (!targetClub.Players.ContainsKey((ulong)accountRow.Id))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is in another clan");
                            return true;
                        }
                        if (await targetClub.RemovePlayer((ulong)accountRow.Id))
                        {
                            plr?.SendConsoleMessage(S4Color.Green +
                                                    $"Removed player {accountRow.Nickname} from clan {targetClub.ClanName}");
                        }
                        return true;
                    }
                case "removestaff":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                            goto default;
                        if (!GameServer.Instance.ClubManager.Any(x => x.Players.ContainsKey((ulong)accountRow.Id)))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is not in a clan");
                            return true;
                        }
                        if (!targetClub.Players.ContainsKey((ulong)accountRow.Id))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is in another clan");
                            return true;
                        }
                        if (await targetClub.ChangeStaffStatus((ulong)accountRow.Id, false))
                        {
                            plr?.SendConsoleMessage(S4Color.Green +
                                                    $"Player {accountRow.Nickname} is now Regular in {targetClub.ClanName}");
                        }
                        return true;
                    }
                case "setstaff":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                            goto default;
                        if (!GameServer.Instance.ClubManager.Any(x => x.Players.ContainsKey((ulong)accountRow.Id)))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is not in a clan");
                            return true;
                        }
                        if (!targetClub.Players.ContainsKey((ulong)accountRow.Id))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is in another clan");
                            return true;
                        }
                        if (await targetClub.ChangeStaffStatus((ulong)accountRow.Id, true))
                        {
                            plr?.SendConsoleMessage(S4Color.Green +
                                                    $"Player {accountRow.Nickname} is now staff in {targetClub.ClanName}");
                        }
                        return true;
                    }
                case "forcemaster":
                    {
                        if (plr.Account.SecurityLevel <= SecurityLevel.GameMaster)
                            goto default;
                        if (!GameServer.Instance.ClubManager.Any(x => x.Players.ContainsKey((ulong)accountRow.Id)))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is not in a clan");
                            return true;
                        }
                        if (!targetClub.Players.ContainsKey((ulong)accountRow.Id))
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Player is in another clan");
                            return true;
                        }
                        if (await targetClub.ForceChangeMaster((ulong)accountRow.Id))
                        {
                            plr?.SendConsoleMessage(S4Color.Green +
                                                    $"Changed Master from clan {targetClub.ClanName} to player {accountRow.Nickname}");
                        }
                        return true;
                    }
                default:
                    {
                        if (plr.Account.SecurityLevel >= SecurityLevel.GameMaster && !memberScope)
                        {
                            plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                            plr.SendConsoleMessage(S4Color.Red + "> /clan forcejoin <username> <clan>");
                            plr.SendConsoleMessage(S4Color.Red + "> /clan forcekick <username> <clan>");
                            plr.SendConsoleMessage(S4Color.Red + "> /clan forcemaster <username> <clan>");
                            plr.SendConsoleMessage(S4Color.Red + "> /clan invite <username>");
                            plr.SendConsoleMessage(S4Color.Red + "> /clan kick <username>");
                        }
                        else
                        {
                            plr?.SendAsync(new MessageChatAckMessage(
                                ChatType.Channel,
                                plr.Account.Id,
                                "ClanMgr",
                                "/clan invite <username>"));
                            plr?.SendAsync(new MessageChatAckMessage(
                                ChatType.Channel,
                                plr.Account.Id,
                                "ClanMgr",
                                "/clan kick <username>"));
                        }
                        return true;
                    }
            }
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

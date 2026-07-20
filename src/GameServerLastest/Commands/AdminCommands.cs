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
    using Santana.Network.Data.Game;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    internal class AdminCommands : ICommand
    {
        public AdminCommands()
        {
            Name = "admin";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[]
            {
                new RenameCommand(), new LevelCommand(),
                new ChangeHPCommand(), new ChangeMPCommand(),
            };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
        {
            return true;
        }
        public string Help()
        {
            var builder = new StringBuilder();
            builder.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                builder.Append("");
                builder.AppendLine(sub.Help());
            }
            return builder.ToString();
        }
        private class RenameCommand : ICommand
        {
            public RenameCommand()
            {
                Name = "rename";
                AllowConsole = true;
                Permission = SecurityLevel.GameMaster;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > rename <nickname> <newname>", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> rename <nickname> <newname>");
                    return true;
                }
                using (var authDb = AuthDatabase.Open())
                {
                    var acc = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                    if (acc == null)
                    {
                        plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                        return false;
                    }
                    plr?.Channel?.SendMessage(plr, "system", "Changed" + acc.Nickname + "'s nickname to " + args[1], NewChatType.Whisper);
                    plr.SendConsoleMessage($"Changed {acc.Nickname}'s nickname to {args[1]}");
                    acc.Nickname = args[1];
                    DbUtil.Update(authDb, acc);
                    var onlineTarget = GameServer.Instance.PlayerManager.Get((ulong)acc.Id);
                    onlineTarget?.Session?.SendAsync(new ItemUseChangeNickAckMessage { Result = 0 });
                    onlineTarget?.Session?.SendAsync(new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class SecurityCommand : ICommand
        {
            public SecurityCommand()
            {
                Name = "seclevel";
                AllowConsole = true;
                Permission = SecurityLevel.Administrator;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > seclevel <nickname> <level>", NewChatType.All);
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> seclevel <nickname> <level>");
                    return true;
                }
                using (var authDb = AuthDatabase.Open())
                {
                    var acc = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                    if (acc == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                        return false;
                    }
                    if (!byte.TryParse(args[1], out var newLevel))
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                        plr.SendConsoleMessage(S4Color.Red + "> seclevel <username> <level>");
                        plr.SendConsoleMessage(S4Color.Red + "> seclevel <level>");
                        return true;
                    }
                    if (plr.Account.SecurityLevel <= (SecurityLevel)acc.SecurityLevel)
                    {
                        plr.SendConsoleMessage($"You cannot change the seclevel of this player");
                        return true;
                    }
                    if (plr.Account.SecurityLevel <= (SecurityLevel)newLevel)
                    {
                        plr.SendConsoleMessage($"Your cannot use a higher level than you have");
                        return true;
                    }
                    plr.SendConsoleMessage($"Changed {acc.Nickname}'s seclevel to {args[1]}");
                    acc.SecurityLevel = newLevel;
                    DbUtil.Update(authDb, acc);
                    var onlineTarget = GameServer.Instance.PlayerManager.Get((ulong)acc.Id);
                    onlineTarget?.Session?.SendAsync(new ItemUseChangeNickAckMessage { Result = 0 });
                    onlineTarget?.Session?.SendAsync(
                        new ServerResultAckMessage(ServerResult.CreateNicknameSuccess));
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class LevelCommand : ICommand
        {
            public LevelCommand()
            {
                Name = "level";
                AllowConsole = false;
                Permission = SecurityLevel.Administrator;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 1)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > level <nickname> <level>", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> level <nickname> <level>");
                    return true;
                }
                if (args.Length >= 2)
                {
                    using (var authDb = AuthDatabase.Open())
                    {
                        var acc = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                            .Include<BanDto>(join => join.LeftOuterJoin())
                            .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                            .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                        if (acc == null)
                        {
                            plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                            plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                            return true;
                        }
                        var profile = (await DbUtil.FindAsync<PlayerDto>(authDb, statement => statement
                                .Where($"{nameof(PlayerDto.Id):C} = @Id")
                                .WithParameters(new { acc.Id }))
                            ).FirstOrDefault();
                        if (profile == null)
                        {
                            plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                            plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                            return true;
                        }
                        if (byte.TryParse(args[1], out var newLevel))
                        {
                            var experienceTable = GameServer.Instance.ResourceCache.GetExperience();
                            if (experienceTable.TryGetValue(newLevel, out var expEntry))
                            {
                                plr?.Channel?.SendMessage(plr, "system", "Changed " + acc.Nickname + "'s level to " + args[1], NewChatType.Whisper);
                                plr.SendConsoleMessage($"Changed {acc.Nickname}'s level to {args[1]}");
                                profile.Level = newLevel;
                                profile.TotalExperience = expEntry.TotalExperience;
                                DbUtil.Update(authDb, profile);
                                var onlineTarget = GameServer.Instance.PlayerManager.Get((ulong)acc.Id);
                                if (onlineTarget != null)
                                {
                                    onlineTarget.Level = newLevel;
                                    onlineTarget.TotalExperience = expEntry.TotalExperience;
                                    onlineTarget.Session?.SendAsync(new ExpRefreshInfoAckMessage(onlineTarget.TotalExperience));
                                    onlineTarget.Session?.SendAsync(
                                        new PlayerAccountInfoAckMessage(onlineTarget.Map<Player, PlayerAccountInfoDto>()));
                                    onlineTarget.NeedsToSave = true;
                                }
                            }
                            else
                            {
                                plr?.Channel?.SendMessage(plr, "system", "Invalid Level", NewChatType.Whisper);
                                plr.SendConsoleMessage(S4Color.Red + "Invalid Level");
                            }
                        }
                        else
                        {
                            plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > level <nickname> <level>", NewChatType.Whisper);
                            plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                            plr.SendConsoleMessage(S4Color.Red + "> level <nickname> <level>");
                        }
                    }
                }
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
       private class ChangeHPCommand : ICommand
        {
            public ChangeHPCommand()
            {
                Name = "chhp";
                AllowConsole = true;
                Permission = SecurityLevel.Administrator;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> chhp <nickname> <hp>");
                    return true;
                }
                if (plr.Room == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "You not in room");
                    return true;
                }
                if (!float.TryParse(args[0], out var newHp))
                    return false;
                var victim = plr;
                if (ulong.TryParse(args[1], out var victimId))
                {
                    victim = plr.Room.Players.GetValueOrDefault(victimId);
                    if (victim == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Player not found in current room");
                        return true;
                    }
                }
                else
                {
                    victim = plr.Room.Players.Values.FirstOrDefault(x =>
                        x.Account.Nickname.Equals(args[1], StringComparison.OrdinalIgnoreCase));
                    if (victim == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Player not found in current room");
                        return true;
                    }
                }
                plr.SendConsoleMessage(S4Color.Green + "Player HP Changed");
                await victim.SendAsync(new AdminChangeHPAckMessage { Value = newHp });
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
        private class ChangeMPCommand : ICommand
        {
            public ChangeMPCommand()
            {
                Name = "chsp";
                AllowConsole = true;
                Permission = SecurityLevel.Administrator;
                SubCommands = new ICommand[] { };
            }
            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }
            public async ValueTask <bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                    plr.SendConsoleMessage(S4Color.Red + "> chsp <nickname> <sp>");
                    return true;
                }
                if (plr.Room == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "You not in room");
                    return true;
                }
                if (!float.TryParse(args[0], out var newSp))
                    return false;
                var victim = plr;
                if (ulong.TryParse(args[1], out var victimId))
                {
                    victim = plr.Room.Players.GetValueOrDefault(victimId);
                    if (victim == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Player not found in current room");
                        return true;
                    }
                }
                else
                {
                    victim = plr.Room.Players.Values.FirstOrDefault(x =>
                        x.Account.Nickname.Equals(args[1], StringComparison.OrdinalIgnoreCase));
                    if (victim == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Player not found in current room");
                        return true;
                    }
                }
                plr.SendConsoleMessage(S4Color.Green + "Player HP Changed");
                await victim.SendAsync(new AdminChangeHPAckMessage { Value = newSp });
                return true;
            }
            public string Help()
            {
                return Name;
            }
        }
    }
}

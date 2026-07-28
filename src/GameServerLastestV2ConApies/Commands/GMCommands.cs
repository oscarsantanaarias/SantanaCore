using Dapper.FastCrud;
using Dapper.FastCrud.Configuration.StatementOptions.Builders;
using Santana;
using Santana.Commands;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Data.Chat;
using Santana.Network.Data.GameRule;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Message.GameRule;
using Santana.Network.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
internal class GMCommands : ICommand
{
    public string Name { get; }
    public bool AllowConsole { get; }
    public SecurityLevel Permission { get; }
    public IReadOnlyList<ICommand> SubCommands { get; }
    public RoomManager RoomManager { get; }
    public GMCommands()
    {
        Name = "gm";
        AllowConsole = true;
        Permission = SecurityLevel.GameSage;
        SubCommands = new ICommand[]
        {
             new GetIDCommand(), new KillRoomCommand(), new SetMasterCommand(),
             new AddAPCommand(), new AddItemCommand(), new AddPENCommand(), new AddNameTagCommand(),
             new NameTagTestCommand()
        };
    }
    public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
    {
        return true;
    }
    public string Help()
    {
        var builder = new StringBuilder();
        builder.AppendLine(Name);
        foreach (ICommand sub in SubCommands)
        {
            builder.Append("");
            builder.AppendLine(sub.Help());
        }
        return builder.ToString();
    }
    private class AddItemCommand : ICommand
    {
        public AddItemCommand()
        {
            Name = "additem";
            AllowConsole = false;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 3)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > additem <nickname> <itemId> <color>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> additem <nickname> <itemId> <color>");
                return true;
            }
            var target = GameServer.Instance.PlayerManager.Get(args[0]);
            if (target == null)
            {
                plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                return false;
            }
            int.TryParse(args[1], out var itemId);
            byte.TryParse(args[2], out var colorValue);
            PlayerItem created = null;
            try
            {
                created = target.Inventory.Create(itemId, 0, colorValue, new EffectNumber[0], 1);
            }
            catch (Exception)
            { }
            if (created == null)
            {
                plr?.Channel?.SendMessage(plr, "system", "Invalid item", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Invalid item");
                return false;
            }
            plr?.Channel?.SendMessage(plr, "system", "Added " + itemId.ToString() + " " + colorValue + " to " + target.Account.Nickname, NewChatType.Whisper);
            plr.SendConsoleMessage($"Added {itemId} {colorValue} to {target.Account.Nickname}");
            return true;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class AddAPCommand : ICommand
    {
        public AddAPCommand()
        {
            Name = "addap";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > addap <nickname> <amount>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> addap <nickname> <amount>");
                return true;
            }
            using (var authDb = AuthDatabase.Open())
            {
                var acc = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                    .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                    .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                var profile = (await DbUtil.FindAsync<PlayerDto>(authDb, statement => statement
                    .Where($"{nameof(PlayerDto.Id):C} = @Id")
                    .WithParameters(new { Id = acc.Id }))).FirstOrDefault();
                if (acc == null || profile == null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                    return false;
                }
                int.TryParse(args[1], out var delta);
                plr?.Channel?.SendMessage(plr, "system", "Added " + args[1] + " AP to " + acc.Nickname, NewChatType.Whisper);
                plr.SendConsoleMessage($"Added {args[1]} AP to {acc.Nickname}");
                profile.AP += delta;
                DbUtil.Update(authDb, profile);
                var onlineTarget = GameServer.Instance.PlayerManager.Get((ulong)profile.Id);
                if (onlineTarget != null)
                {
                    onlineTarget.AP += (uint)delta;
                    await onlineTarget.SendAsync(new MoneyRefreshCashInfoAckMessage(onlineTarget.PEN, onlineTarget.AP));
                }
            }
            return true;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class AddPENCommand : ICommand
    {
        public AddPENCommand()
        {
            Name = "addpen";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > addpen <nickname> <amount>", NewChatType.Whisper);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> addpen <nickname> <amount>");
                return true;
            }
            using (var authDb = AuthDatabase.Open())
            {
                var acc = (await DbUtil.FindAsync<AccountDto>(authDb, statement => statement
                    .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                    .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                var profile = (await DbUtil.FindAsync<PlayerDto>(authDb, statement => statement
                    .Where($"{nameof(PlayerDto.Id):C} = @Id")
                    .WithParameters(new { Id = acc.Id }))).FirstOrDefault();
                if (acc == null || profile == null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.All);
                    plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                    return false;
                }
                int.TryParse(args[1], out var delta);
                plr?.Channel?.SendMessage(plr, "system", "Added " + args[1] + " PEN to " + acc.Nickname, NewChatType.Whisper);
                plr.SendConsoleMessage($"Added {args[1]} PEN to {acc.Nickname}");
                profile.PEN += delta;
                DbUtil.Update(authDb, profile);
                var onlineTarget = GameServer.Instance.PlayerManager.Get((ulong)profile.Id);
                if (onlineTarget != null)
                {
                    onlineTarget.PEN += (uint)delta;
                    await onlineTarget.SendAsync(new MoneyRefreshCashInfoAckMessage(onlineTarget.PEN, onlineTarget.AP));
                }
            }
            return true;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class AddNameTagCommand : ICommand
    {
        public AddNameTagCommand()
        {
            Name = "nametag";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[] { };
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> addnametag <nickname> <tagid>");
                return true;
            }
            if (args.Length >= 2)
            {
                using (var db = AuthDatabase.Open())
                {
                    var accountDto = (await DbUtil.FindAsync<AccountDto>(db, statement => statement
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = args[0] }))).FirstOrDefault();
                    var playerDto = (await DbUtil.FindAsync<PlayerDto>(db, statement => statement
                        .Where($"{nameof(PlayerDto.Id):C} = @Id")
                        .WithParameters(new { Id = accountDto.Id }))).FirstOrDefault();
                    if (accountDto == null || playerDto == null)
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                        return false;
                    }
                    uint.TryParse(args[1], out var tag);
                    plr.SendConsoleMessage($"Applied NameTag to {accountDto.Nickname}");
                    playerDto.TagId = tag;
                    DbUtil.Update(db, playerDto);
                    var player = GameServer.Instance.PlayerManager.Get((ulong)playerDto.Id);
                    if (player != null)
                    {
                        player.NameTag = tag;
                        player.CharacterManager.Boosts.PlayerNameTag();
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
    private class NameTagTestCommand : ICommand
    {
        public NameTagTestCommand()
        {
            Name = "nametagtest";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = Array.Empty<ICommand>();
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        private static void Reply(Player plr, string message, bool error = false)
        {
            if (plr != null)
            {
                plr.SendConsoleMessage((error ? S4Color.Red : S4Color.Green) + message);
            }
            else
            {
                if (error)
                    CommandManager.Logger.Error(message);
                else
                    CommandManager.Logger.Information(message);
            }
        }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                Reply(plr, "Wrong Usage, possible usages:", true);
                Reply(plr, "gm nametagtest <nickname> <rawTagId>", true);
                Reply(plr, "gm nametagtest <nickname> clear", true);
                return true;
            }
            var player = GameServer.Instance.PlayerManager.Get(args[0]);
            if (player == null)
            {
                Reply(plr, "Unknown player", true);
                return false;
            }
            uint rawTagId = 0;
            if (!args[1].Equals("clear", StringComparison.InvariantCultureIgnoreCase) &&
                !uint.TryParse(args[1], out rawTagId))
            {
                Reply(plr, "Invalid tag id", true);
                return false;
            }
            player.CollectBookNameTag = rawTagId;
            player.NameTag = rawTagId;
            AuthService.LoadPlayerNameTag(player, true, false);
            player.CharacterManager.Boosts.PlayerNameTag();
            Reply(plr, $"NameTag test -> {player.Account.Nickname} raw={rawTagId}");
            await Task.CompletedTask;
            return true;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class SetMasterCommand : ICommand
    {
        public SetMasterCommand()
        {
            Name = "setmaster";
            AllowConsole = false;
            Permission = SecurityLevel.GameMaster;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length > 0)
            {
                var chosen = GameServer.Instance.PlayerManager.Get(args[0]);
                if (chosen != null)
                    if (chosen.Room != null)
                    {
                        chosen.Room.ChangeMasterIfNeeded(chosen, true);
                        chosen.Room.ChangeHostIfNeeded(chosen, true);
                        plr.SendConsoleMessage($"\"{chosen.Account.Nickname}\" is master now");
                    }
                    else
                    {
                        plr.SendConsoleMessage(S4Color.Red + "Player is not in a room!");
                    }
            }
            else
            {
                if (plr.Room == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "You are not in a room!");
                }
                else
                {
                    plr.Room.ChangeMasterIfNeeded(plr, true);
                    plr.Room.ChangeHostIfNeeded(plr, true);
                    plr.SendConsoleMessage($"You are master now");
                }
            }
            return true;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class GetIDCommand : ICommand
    {
        public GetIDCommand()
        {
            Name = "getid";
            AllowConsole = true;
            Permission = SecurityLevel.Administrator;
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
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> getid <nickname>");
                return true;
            }
            var chosen = GameServer.Instance.PlayerManager.Get(args[0]);
            if (chosen != null)
            {
                if (plr != null)
                    plr.SendConsoleMessage(S4Color.Green +
                                           $"> {chosen.Account.Nickname}'s id is {chosen.Account.Id}");
                else
                    CommandManager.Logger.Information(
                        $"Nickname {chosen.Account.Nickname} resolves to account {chosen.Account.Id}");
                return true;
            }
            plr.SendConsoleMessage(S4Color.Red + "Unknown player!");
            return false;
        }
        public string Help()
        {
            return Name;
        }
    }
    private class KillRoomCommand : ICommand
    {
        public KillRoomCommand()
        {
            Name = "killroom";
            AllowConsole = true;
            Permission = SecurityLevel.Administrator;
            SubCommands = new ICommand[0];
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> killroom channelid roomid");
                return true;
            }
            if (!uint.TryParse(args[0], out var channelId))
                return false;
            if (!uint.TryParse(args[1], out var roomId))
                return false;
            var channel = server.ChannelManager[channelId];
            var room = channel?.RoomManager[roomId];
            if (room != null)
            {
                foreach (var member in room.Players.Values)
                    member.Room.Leave(member, RoomLeaveReason.ModeratorKick);
                room.RoomManager.Remove(room);
                return true;
            }
            return false;
        }
        public string Help()
        {
            return Name;
        }
    }
}

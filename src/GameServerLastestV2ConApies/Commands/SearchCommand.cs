using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Network;

namespace Santana.Commands
{
    internal class SearchCommand : ICommand
    {
        public SearchCommand()
        {
            Name = "/search";
            AllowConsole = true;
            Permission = SecurityLevel.GameSage;
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
                plr?.Channel?.SendMessage(plr, "system", "Wrong Usage, possible usages: > /search <username>", NewChatType.All);
                plr.SendConsoleMessage(S4Color.Red + "Wrong Usage, possible usages:");
                plr.SendConsoleMessage(S4Color.Red + "> /search <username>");
                return true;
            }

            var wantedName = args[0];
            using (var auth = AuthDatabase.Open())
            {
                var record = (await DbUtil.FindAsync<AccountDto>(auth, statement => statement
                        .Include<BanDto>(join => join.LeftOuterJoin())
                        .Where($"{nameof(AccountDto.Nickname):C} = @Nickname")
                        .WithParameters(new { Nickname = wantedName }))
                    ).FirstOrDefault();

                if (record == null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Unknown player", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Unknown player");
                    return true;
                }

                var target = GameServer.Instance.PlayerManager.Get((ulong)record.Id);
                if (target == null)
                {
                    plr?.Channel?.SendMessage(plr, "system", "Player is not online", NewChatType.Whisper);
                    plr.SendConsoleMessage(S4Color.Red + "Player is not online");
                    return true;
                }

                if (!(target.Channel?.Id > 0))
                {
                    plr?.Channel?.SendMessage(plr, "system", target.Account.Nickname + " is waiting in server", NewChatType.Whisper);

                    plr.SendConsoleMessage(
              $"\"{target.Account.Nickname}\"is waiting in server \"{Config.Instance.Name}\"now");
                    return true;
                }

                if (target.Room != null)
                {
                    plr?.Channel?.SendMessage(plr, "system", target.Account.Nickname + " is connecting to the room " + target.Room.Id + " in channel " + target.Channel.Id, NewChatType.All);
                    plr.SendConsoleMessage(
                $"\"{target.Account.Nickname}\"is connecting to the room \"{target.Room.Id}\"in channel \"{target.Channel.Id}\"now");
                }
                else
                {
                    plr?.Channel?.SendMessage(plr, "system", target.Account.Nickname + " is waiting in channel " + target.Channel.Id, NewChatType.Whisper);

                    plr.SendConsoleMessage(
                $"\"{target.Account.Nickname}\"is waiting in channel {target.Channel.Id} now");
                }
            }

            return true;
        }

        public string Help()
        {
            return new UserkickCommand().Help();
        }
    }
}

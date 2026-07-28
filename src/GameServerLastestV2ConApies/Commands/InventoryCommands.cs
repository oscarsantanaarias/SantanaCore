using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Santana.Network;
using Newtonsoft.Json;

namespace Santana.Commands
{
    internal class InventoryCommands : ICommand
    {
        public InventoryCommands()
        {
            Name = "inventory";
            AllowConsole = false;
            Permission = SecurityLevel.Developer;
            SubCommands = new ICommand[] { new ListCommand(), new ShowItemCommand(), new SetCommand() };
        }

        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }

        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            return true;
        }

        public string Help()
        {
            var help = new StringBuilder();
            help.AppendLine(Name);
            foreach (var sub in SubCommands)
            {
                help.Append("");
                help.AppendLine(sub.Help());
            }

            return help.ToString();
        }

        private class ListCommand : ICommand
        {
            public ListCommand()
            {
                Name = "list";
                AllowConsole = false;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[0];
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                var itemTable = GameServer.Instance.ResourceCache.GetItems();
                var lines = new StringBuilder();
                foreach (var owned in plr.Inventory)
                {
                    var info = itemTable.GetValueOrDefault(owned.ItemNumber);
                    lines.AppendLine($"#{owned.Id}: {owned.ItemNumber} {info?.Name}");
                }

                plr.SendConsoleMessage(lines.ToString());
                return true;
            }

            public string Help()
            {
                return Name;
            }
        }

        private class ShowItemCommand : ICommand
        {
            public ShowItemCommand()
            {
                Name = "showitem";
                AllowConsole = false;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[0];
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 1)
                    return false;

                if (!ulong.TryParse(args[0], out var targetId))
                    return false;

                var found = plr.Inventory[targetId];
                if (found == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Item not found");
                    return true;
                }

                plr.SendConsoleMessage(JsonConvert.SerializeObject(found, Formatting.Indented));
                return true;
            }

            public string Help()
            {
                return Name + "<id>";
            }
        }

        private class SetCommand : ICommand
        {
            public SetCommand()
            {
                Name = "set";
                AllowConsole = false;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[0];
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                    return false;

                if (!ulong.TryParse(args[0], out var targetId))
                    return false;

                var found = plr.Inventory[targetId];
                if (found == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Item not found");
                    return true;
                }

                switch (args[1].ToLower())
                {
                    case "durability":
                        if (!int.TryParse(args[2], out var durabilityValue))
                            return false;
                        break;

                    default:
                        plr.SendConsoleMessage(S4Color.Red + "Invalid field");
                        break;
                }
                return true;
            }

            public string Help()
            {
                return Name + "<id> <field> <value>";
            }
        }

        private class CreateItemCommand : ICommand
        {
            public CreateItemCommand()
            {
                Name = "create";
                AllowConsole = false;
                Permission = SecurityLevel.Developer;
                SubCommands = new ICommand[0];
            }

            public string Name { get; }
            public bool AllowConsole { get; }
            public SecurityLevel Permission { get; }
            public IReadOnlyList<ICommand> SubCommands { get; }

            public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
            {
                if (args.Length < 2)
                    return false;

                var shop = GameServer.Instance.ResourceCache.GetShop();

                int itemId;
                byte color;
                if (!int.TryParse(args[0], out itemId))
                    return false;
                if (!byte.TryParse(args[1], out color))
                    return false;

                if (shop.GetItem(itemId).ItemNumber == null)
                {
                    plr.SendConsoleMessage(S4Color.Red + "Item not found");
                    return false;
                }
                else
                {
                    plr.Inventory.Create(itemId, 3, color, new EffectNumber[0], 1);
                    plr.SendConsoleMessage(S4Color.Green + "Item Created");
                }

                return true;
            }

            public string Help()
            {
                return Name;
            }
        }
    }
}

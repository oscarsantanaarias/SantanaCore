using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Santana.Network;
using Santana.Network.Data.Game;
using Santana.Network.Message.Game;
namespace Santana.Commands
{
    internal class ArcadeProbeCommand : ICommand
    {
        public ArcadeProbeCommand()
        {
            Name = "/arcadeprobe";
            AllowConsole = true;
            Permission = SecurityLevel.GameMaster;
            SubCommands = Array.Empty<ICommand>();
        }
        public string Name { get; }
        public bool AllowConsole { get; }
        public SecurityLevel Permission { get; }
        public IReadOnlyList<ICommand> SubCommands { get; }
        public async ValueTask<bool> Execute(GameServer server, Player plr, string[] args)
        {
            if (args.Length < 2)
            {
                CommandManager.Logger.Information(Help());
                return true;
            }
            var targets = ResolveTargets(args[0]);
            if (targets.Count == 0)
            {
                CommandManager.Logger.Information($"[arcadeprobe] target no online: {args[0]}");
                return true;
            }
            var mode = args[1].Trim().ToLowerInvariant();
            ArcadeStageInfoDto[] infos;
            if (mode == "iso")
            {
                infos = Enumerable.Range(1, 8).Select(s =>
                {
                    var d = new ArcadeStageInfoDto { Unk1 = (uint)s };
                    SetField(d, s + 1, 1);
                    return d;
                }).ToArray();
            }
            else if (mode == "field" && args.Length >= 4 && int.TryParse(args[2], out var f) && uint.TryParse(args[3], out var v))
            {
                infos = Enumerable.Range(1, 8).Select(s =>
                {
                    var d = new ArcadeStageInfoDto { Unk1 = (uint)s };
                    SetField(d, f, v);
                    return d;
                }).ToArray();
            }
            else if (mode == "all" && args.Length >= 3 && uint.TryParse(args[2], out var av))
            {
                infos = Enumerable.Range(1, 8).Select(s =>
                {
                    var d = new ArcadeStageInfoDto { Unk1 = (uint)s };
                    for (var i = 2; i <= 13; i++)
                        SetField(d, i, av);
                    return d;
                }).ToArray();
            }
            else
            {
                CommandManager.Logger.Information(Help());
                return true;
            }
            foreach (var t in targets)
                await t.SendAsync(new PlayerArcadeStageInfoAckMessage { Infos = infos });
            CommandManager.Logger.Information($"[arcadeprobe] 1052 enviado mode={mode} a {targets.Count} player(s), {infos.Length} stages");
            return true;
        }
        private static List<Player> ResolveTargets(string wanted)
        {
            if (string.Equals(wanted, "all", StringComparison.OrdinalIgnoreCase))
                return GameServer.Instance.PlayerManager.Where(x => x?.Account != null).ToList();
            var one = GameServer.Instance.PlayerManager.FirstOrDefault(x =>
                x?.Account != null &&
                (string.Equals(x.Account.Nickname, wanted, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(x.Account.Username, wanted, StringComparison.OrdinalIgnoreCase) ||
                 (ulong.TryParse(wanted, out var id) && (ulong)x.Account.Id == id)));
            return one == null ? new List<Player>() : new List<Player> { one };
        }
        private static void SetField(ArcadeStageInfoDto d, int index, uint value)
        {
            switch (index)
            {
                case 1: d.Unk1 = value; break;
                case 2: d.Unk2 = value; break;
                case 3: d.Unk3 = value; break;
                case 4: d.Unk4 = value; break;
                case 5: d.Unk5 = value; break;
                case 6: d.Unk6 = value; break;
                case 7: d.Unk7 = value; break;
                case 8: d.Unk8 = value; break;
                case 9: d.Unk9 = value; break;
                case 10: d.Unk10 = value; break;
                case 11: d.Unk11 = value; break;
                case 12: d.Unk12 = value; break;
                case 13: d.Unk13 = (byte)value; break;
            }
        }
        public string Help() =>
            "/arcadeprobe <target|all> iso                 -> 8 stages, stage s con Unk{s+1}=1 (aisla campo)\n" +
            "/arcadeprobe <target|all> field <idx> <value> -> 8 stages, Unk1=stage, Unk{idx}=value\n" +
            "/arcadeprobe <target|all> all <value>         -> 8 stages, Unk1=stage, todos los otros=value";
    }
}

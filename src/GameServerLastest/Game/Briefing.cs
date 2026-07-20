using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SantanaLib.IO;
using Santana;
using Santana.Game.GameRules;

namespace Santana.Game
{
    internal class Briefing
    {
        public Briefing(GameRuleBase gameRule)
        {
            GameRule = gameRule;
        }

        public GameRuleBase GameRule { get; }

        public virtual PlayerTeam GetWinnerTeam()
        {
            var topScore = GameRule.Room.TeamManager.Values.Max(t => t.Score);
            var leaders = GameRule.Room.TeamManager.Values.Where(t => t.Score == topScore).ToArray();

            if (leaders.Length > 1)
            {
                var summedByTeam = new Dictionary<Team, uint>();
                foreach (var candidate in leaders)
                {
                    var total = candidate.PlayersPlaying.Sum(p => p.RoomInfo.Stats.TotalScore);
                    summedByTeam.Add(candidate.Team, (uint)total);
                }

                topScore = summedByTeam.Values.Max();
                leaders = GameRule.Room.TeamManager.Values.Where(t => summedByTeam[t.Team] == topScore).ToArray();
            }

            return leaders[0];
        }

        public virtual void SerializeTeam(PlayerTeam team, BinaryWriter w)
        {
            w.WriteEnum(team.Team);

            if (GameRule.GameRule == Santana.GameRule.Siege)
            {
                w.Write(team.Score);

                w.Write((byte)0);
                w.Write((byte)0);

                w.Write((byte)0);
                w.Write((byte)0);
            }

            if (GameRule.GameRule == Santana.GameRule.Arcade)
            {
                w.Write((uint)0);
            }

            w.Write(team.Score);
        }

        protected virtual void Serialize(BinaryWriter w, bool isResult)
        {
            var activePlayers = GameRule.Room.TeamManager.Players.ToArray();
            var watchers = GameRule.Room.TeamManager.Spectators.ToArray();

            w.Write((int)GetWinnerTeam().Team);

            w.Write(GameRule.Room.TeamManager.Count);
            w.Write(activePlayers.Length);
            w.Write(watchers.Length);

            foreach (var team in GameRule.Room.TeamManager.Values)
                SerializeTeam(team, w);

            foreach (var participant in activePlayers)
                participant.RoomInfo.Stats.Serialize(w, isResult);

            foreach (var watcher in watchers)
            {
                w.Write(watcher.Account.Id);
                w.Write((long)0);
            }
        }

        public byte[] SerializeDataToArray(bool isResult)
        {
            using (var w = new MemoryStream().ToBinaryWriter(false))
            {
                Serialize(w, isResult);
                return w.ToArray();
            }
        }
    }
}

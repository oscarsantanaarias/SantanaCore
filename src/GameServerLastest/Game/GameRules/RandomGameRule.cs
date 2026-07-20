using Santana.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace Santana.Game.GameRules
{
    internal class RandomGameRule : GameRuleBase
    {

        public RandomGameRule(Room room)
     : base(room)
        {

            var pick = new SecureRandom().Next(1, 5);
            room.Options.IsRandomMode = true;
            room.Options.IsRandom = true;

            switch (pick)
            {
                case 1:
                    room.Options.GameRule = GameRule.Touchdown;
                    break;
                case 2:
                    room.Options.GameRule = GameRule.Chaser;
                    break;
                case 3:
                    room.Options.GameRule = GameRule.Deathmatch;
                    break;
                case 4:
                    room.Options.GameRule = GameRule.BattleRoyal;
                    break;
                case 5:
                    room.Options.GameRule = GameRule.Captain;
                    break;
                case 6:
                    room.Options.GameRule = GameRule.Siege;
                    break;
            }

            foreach (var member in room.Players.Values)
            {
                member.stats.OnJoin(room.RoomManager.GameRuleFactory.Get(room.Options.GameRule, room));
                member.RoomInfo.IsReady = false;

                room.GameRuleManager.MapInfo = GameServer.Instance.ResourceCache.GetMaps()[room.Options.MapId];
                room.GameRuleManager.GameRule = room.RoomManager.GameRuleFactory.Get(room.Options.GameRule, room);
            }

        }

        public override GameRule GameRule => GameRule.Random;
        public override bool CountMatch => true;
        public override Briefing Briefing { get; }

        public override PlayerRecord GetPlayerRecord(Player plr)
        {
            throw new NotImplementedException();
        }
    }
}

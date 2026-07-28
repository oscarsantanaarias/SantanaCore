using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Santana;
using Santana.Game.GameRules;

namespace Santana.Game
{
    internal class GameRuleFactory
    {
        private readonly IDictionary<GameRule, Func<Room, GameRuleBase>> _ruleBuilders =
            new ConcurrentDictionary<GameRule, Func<Room, GameRuleBase>>();

        public GameRuleFactory()
        {
            Add(GameRule.Touchdown, r => new TouchdownGameRule(r));
            Add(GameRule.Deathmatch, r => new DeathmatchGameRule(r));
            Add(GameRule.Chaser, r => new ChaserGameRule(r));
            Add(GameRule.BattleRoyal, r => new BattleRoyalGameRule(r));
            Add(GameRule.Captain, r => new CaptainGameRule(r));
            Add(GameRule.Siege, r => new SiegeGameRule(r));
            Add(GameRule.Arcade, r => new ArcadeGameRule(r));
            Add(GameRule.Arena, r => new ArenaGameRule(r));

            Add(GameRule.SnowballFight, r => new SnowballFightGameRule(r));
            Add(GameRule.PassTouchdown, r => new PassTouchdownGameRule(r));

            Add(GameRule.Practice, r => new PracticeGameRule(r));
            Add(GameRule.Horde, r => new ConquestGameRule(r));
            Add(GameRule.CombatTrainingTD, r => new TouchdownTrainingGameRule(r));
            Add(GameRule.CombatTrainingDM, r => new DeathmatchTrainingGameRule(r));
            Add(GameRule.Random, r => new RandomGameRule(r));
            Add(GameRule.Warfare, r => new WarfareGameRule(r));
        }

        public void Add(GameRule gameRule, Func<Room, GameRuleBase> gameRuleFactory)
        {
            if (_ruleBuilders.TryAdd(gameRule, gameRuleFactory))
                return;

            throw new Exception($"GameRule {gameRule} already registered");
        }

        public void Remove(GameRuleBase gameRule)
        {
            _ruleBuilders.Remove(gameRule.GameRule);
        }

        public GameRuleBase Get(GameRule gameRule, Room room)
        {
            if (_ruleBuilders.TryGetValue(gameRule, out var builder))
                return builder(room);

            throw new Exception($"GameRule {gameRule} not registered");
        }

        public bool Contains(GameRule gameRule)
        {
            return _ruleBuilders.ContainsKey(gameRule);
        }
    }
}

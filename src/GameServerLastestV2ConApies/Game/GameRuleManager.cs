using System;
using Santana.Game.GameRules;
using Santana.Network;
using Santana.Resource;

namespace Santana.Game
{
    internal class GameRuleManager
    {
        private GameRuleBase _activeRule;
        private MapInfo _activeMap;

        public GameRuleManager(Room room)
        {
            Room = room;
        }

        public Room Room { get; }

        public GameRuleBase GameRule
        {
            get => _activeRule;
            set
            {
                if (value == _activeRule)
                    return;

                _activeRule?.Cleanup();
                _activeRule = value;
                _activeRule?.Initialize();
                OnGameRuleChanged();
            }
        }

        public MapInfo MapInfo
        {
            get => _activeMap;
            set
            {
                if (value == _activeMap)
                    return;

                _activeMap = value;
                OnMapInfoChanged();
            }
        }

        public event EventHandler GameRuleChanged;
        public event EventHandler MapInfoChanged;

        protected virtual void OnGameRuleChanged()
        {
            GameRuleChanged?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void OnMapInfoChanged()
        {
            MapInfoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Update(TimeSpan delta)
        {
            GameRule?.Update(delta);
        }
    }
}

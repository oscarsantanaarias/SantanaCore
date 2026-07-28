namespace Santana
{
    using System;
    using System.Collections.Generic;
    using System.Text;

    using Santana.Network.Message.GameRule;

    internal class PlayerLuckyShot
    {
        private const int TriggerChance = 50;
        private const int RewardAmount = 30;

        private readonly Random _rng = new Random();

        public Player Player { get; internal set; }

        public int BonusExp { get; internal set; }

        public int BonusPen { get; internal set; }

        public PlayerLuckyShot(Player plr)
        {
            Player = plr;
            BonusExp = 0;
            BonusPen = 0;
        }

        public void TryShot(LuckyShotType shotType)
        {
            var roll = _rng.Next(100);
            if (roll <= TriggerChance)
                return;

            Player.SendAsync(new GameLuckyShotAckMessage() { LuckyShotType = shotType, Value = RewardAmount, Unk3 = 0 });

            if (shotType == LuckyShotType.EXP)
                BonusExp += RewardAmount;
            else if (shotType == LuckyShotType.PEN)
                BonusPen += RewardAmount;
        }

        public void Clear()
        {
            BonusExp = 0;
            BonusPen = 0;
        }
    }
}

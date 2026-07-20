namespace Santana
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using Santana.Network.Message.Game;
    using Santana.Network.Message.GameRule;
    using Serilog;
    using Serilog.Core;

    internal class PlayerCoinBuff
    {
        private static readonly ILogger Logger = Log.ForContext(Constants.SourceContextPropertyName, nameof(PlayerCoinBuff));
        private static readonly int _expireAt = 550;

        public Player Player { get; internal set; }

        public CoinBuff[] CoinBuffs { get; set; }

        public PlayerCoinBuff(Player player)
        {
            Player = player;

            CoinBuffs = new CoinBuff[]
            {
        new CoinBuff(BuffType.PEN),
        new CoinBuff(BuffType.EXP),
        new CoinBuff(BuffType.Respawn),
        new CoinBuff(BuffType.Tracking),
        new CoinBuff(BuffType.HP),
        new CoinBuff(BuffType.SP)
            };
        }

        public void StartBuffSystem(BuffType type)
        {
            const byte durationSec = 60;
            var cost = 0;
            var amount = 0;
            var roll = new SecureRandom();

            if (type == BuffType.Respawn && (Player.Room.Options.GameRule == GameRule.Chaser || Player.Room.Options.GameRule == GameRule.Touchdown))
                return;

            switch (type)
            {
                case BuffType.PEN:
                    cost = 50;
                    amount = roll.Next(10, 60);
                    break;
                case BuffType.Respawn:
                    cost = 150;
                    break;
                case BuffType.Tracking:
                    cost = 50;
                    break;
                case BuffType.EXP:
                    cost = 50;
                    amount = roll.Next(10, 60);
                    break;
                case BuffType.HP:
                    cost = 100;
                    amount = 5;
                    break;
                case BuffType.SP:
                    cost = 100;
                    amount = 5;
                    break;
            }

            CoinBuff slot = FindBuff(type);
            if (slot == null)
            { return; }

            if (slot.IsEnabled == true)
            {
                Player.Session.SendAsync(new MoneyUseCoinAckMessage { Message = UseCoinMessage.UnableToUse });
                return;
            }

            if (Player.PEN < cost)
            {
                Player.Session.SendAsync(new MoneyUseCoinAckMessage { Message = UseCoinMessage.InsufficientCoin });
                return;
            }

            slot.IsEnabled = true;

            Player.PEN -= (uint)cost;
            Player.SendAsync(new MoneyRefreshCashInfoAckMessage { PEN = Player.PEN, AP = Player.AP });
            Player.SendAsync(new MoneyUseCoinAckMessage
            {
                Message = UseCoinMessage.Ok,
                BuffType = type,
                Time = durationSec,
                Value = amount,
                Unk5 = 0
            });
        }

        public CoinBuff FindBuff(BuffType type)
        {
            for (int i = 0; i < CoinBuffs.Length; i++)
            {
                if (CoinBuffs[i].Type == type)
                { return CoinBuffs[i]; }
            }

            return null;
        }

        public void Update(int value)
        {
            for (var i = 0; i < CoinBuffs.Length; i++)
            {
                if (CoinBuffs[i].Type == BuffType.Respawn)
                {
                    CoinBuffs[i].CurrentTime = 0;
                    CoinBuffs[i].IsEnabled = false;
                    return;
                }

                if (CoinBuffs[i].IsEnabled)
                {
                    CoinBuffs[i].CurrentTime += value;
                }

                if (CoinBuffs[i].CurrentTime >= _expireAt)
                {
                    CoinBuffs[i].CurrentTime = 0;
                    CoinBuffs[i].IsEnabled = false;
                }
            }
        }

        public void Reset()
        {
            for (var i = 0; i < CoinBuffs.Length; i++)
            {
                CoinBuffs[i].CurrentTime = 0;
                CoinBuffs[i].IsEnabled = false;
            }
        }
    }

    internal class CoinBuff
    {
        public BuffType Type { get; set; }

        public long CurrentTime { get; set; }

        public bool IsEnabled { get; set; }

        public CoinBuff()
        { }

        public CoinBuff(BuffType type)
        {
            Type = type;
            CurrentTime = 0;
            IsEnabled = false;
        }
    }
}

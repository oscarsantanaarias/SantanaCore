using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Santana;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Message.Chat;
using Santana.Network.Services;

namespace Santana.Game
{
    internal abstract class PlayerRecord
    {
        protected PlayerRecord(Player player)
        {
            Player = player;
            Player.RoomInfo.Stats = this;
        }

        public Player Player { get; }
        public abstract uint TotalScore { get; }
        public uint Kills { get; set; }
        public uint KillAssists { get; set; }
        public uint Suicides { get; set; }
        public uint Deaths { get; set; }
        public uint Play { get; set; }
        public uint TD { get; set; }
        public uint ChaserCount { get; set; }
        public uint ChaserKilled { get; set; }
        public uint ChaserSurvived { get; set; }
        public bool FirstKill { get; set; }

        public virtual uint GetPenGain(out uint bonusPen)
        {
            bonusPen = 0;
            var baseExp = GetExpGain(out var expBonus);
            if (expBonus > baseExp)
                return (uint)(baseExp + expBonus / 2);
            return (uint)(baseExp - expBonus / 2);
        }

        public virtual int GetExpGain(out int bonusExp)
        {
            bonusExp = 0;

            var gameCfg = Config.Instance.Game;
            ExperienceRates rates = null;

            switch (Player.Room.GameRuleManager.GameRule.GameRule)
            {
                case GameRule.BattleRoyal:
                    rates = gameCfg.BRExpRates;
                    break;
                case GameRule.Captain:
                    rates = gameCfg.CaptainExpRates;
                    break;
                case GameRule.Chaser:
                    rates = gameCfg.ChaserExpRates;
                    break;
                case GameRule.Deathmatch:
                    rates = gameCfg.DeathmatchExpRates;
                    break;
                case GameRule.PassTouchdown:
                    rates = gameCfg.TouchdownExpRates;
                    break;
                case GameRule.Siege:
                    rates = gameCfg.SiegeExpRates;
                    break;
                case GameRule.SnowballFight:
                    rates = gameCfg.TouchdownExpRates;
                    break;
                case GameRule.Touchdown:
                    rates = gameCfg.TouchdownExpRates;
                    break;
                case GameRule.Warfare:
                    rates = gameCfg.WarfareExpRates;
                    break;

                case GameRule.Arena:
                    rates = gameCfg.DeathmatchExpRates;
                    break;

                case GameRule.Arcade:
                case GameRule.Challenge:
                case GameRule.CombatTrainingDM:
                case GameRule.CombatTrainingTD:
                case GameRule.Horde:
                case GameRule.Practice:
                case GameRule.SemiTouchdown:
                case GameRule.Survival:
                case GameRule.Tutorial:
                    break;
            }

            if (rates == null)
                return 0;

            var contenders = Player.Room.TeamManager.Players
                .Where(p => p.RoomInfo.State == PlayerState.Waiting &&
                            p.RoomInfo.Mode == PlayerGameMode.Normal)
                .ToArray();

            var standing = 1;
            try
            {
                foreach (var contender in contenders.OrderByDescending(p => p.RoomInfo.Stats.TotalScore))
                {
                    if (contender == Player)
                        break;

                    standing++;
                    if (standing > 3)
                        break;
                }
            }
            catch { }

            var placeMultiplier = 1.0f;
            switch (standing)
            {
                case 1:
                    placeMultiplier += rates.FirstPlaceBonus / 100.0f;
                    break;

                case 2:
                    placeMultiplier += rates.SecondPlaceBonus / 100.0f;
                    break;

                case 3:
                    placeMultiplier += rates.ThirdPlaceBonus / 100.0f;
                    break;
            }

            var fromTime = rates.ExpPerMin * Player.RoomInfo.PlayTime.Minutes;
            var fromHeadcount = contenders.Length * rates.PlayerCountFactor;
            var fromScore = rates.ExpPerMin * TotalScore;

            var raw = (fromTime + fromHeadcount + fromScore) * placeMultiplier;

            bonusExp = (int)raw;

            return (int)Math.Round(bonusExp * 0.01);
        }

        public virtual void Reset()
        {
            Kills = 0;
            KillAssists = 0;
            Suicides = 0;
            Deaths = 0;
            TD = 0;
            Play = 0;
            Player.LuckyShot.Clear();
            ChaserCount = 0;
            ChaserKilled = 0;
            ChaserSurvived = 0;
        }

        public virtual void Serialize(BinaryWriter w, bool isResult)
        {
            w.Write(Player.Account.Id);
            w.Write((byte)Player.RoomInfo.Team.Team);
            w.Write((byte)Player.RoomInfo.State);
            w.Write(Convert.ToByte(Player.RoomInfo.IsReady));
            w.Write((int)Player.RoomInfo.Mode);
            w.Write(TotalScore);
            w.Write(0);

            uint bonusPen = 0;
            int bonusExp = 0;
            var leveledUp = false;

            if (isResult && Player.RoomInfo.State != PlayerState.Lobby)
            {
                var penAward = GetPenGain(out bonusPen);
                var expAward = GetExpGain(out bonusExp);

                if (Player.Room.Options.IsFriendly)
                {
                    expAward = 0;
                    penAward = 0;

                    bonusExp = 0;
                    bonusPen = 0;
                }

                var penMultiplier = Player.CharacterManager.Boosts.GetPenRate() + 1f;
                var expMultiplier = Player.CharacterManager.Boosts.GetExpRate() + 1f;

                penAward += (uint)Player.LuckyShot.BonusPen;
                expAward += (int)Player.LuckyShot.BonusExp;

                bonusPen += (uint)Player.LuckyShot.BonusPen;
                bonusExp += (int)Player.LuckyShot.BonusExp;

                w.Write(penAward);
                w.Write(expAward);

                Player.PEN += (penAward * (uint)penMultiplier + bonusPen);

                leveledUp = Player?.GainExp(expAward * (int)expMultiplier + bonusExp) ?? false;
            }
            else
            {
                w.Write(0);
                w.Write(0);
            }

            w.Write(Player.TotalExperience);
            w.Write(leveledUp);
            w.Write(bonusExp);
            w.Write(bonusPen);
            w.Write(0);

            w.Write(0);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
            w.Write(0);

            w.Write((byte)0);
            w.Write(0);
            w.Write(0);
            w.Write(0);
        }
    }
}

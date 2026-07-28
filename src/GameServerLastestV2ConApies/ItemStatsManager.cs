using Santana.Database.Game;
using Santana.Network.Data.Chat;
using System;
using System.Collections.Generic;
using System.Text;

namespace Santana
{
    internal class ItemStatsManager
    {
        private readonly Player _owner;

        public ItemStatsManager(Player player)
        {
            _owner = player;
            Clothes = new ClotheStats(player);
            Weapons = new WeaponsStats(player);
            Skill = new SkillStats(player);
        }

        public ClotheStats Clothes { get; }
        public WeaponsStats Weapons { get; }
        public SkillStats Skill { get; }

        public ClotheStats GetClothesStats()
        {
            return Clothes;
        }

        public WeaponsStats GetWeaponsStats()
        {
            return Weapons;
        }

        public SkillStats GetSkillStats()
        {
            return Skill;
        }
    }

    internal abstract class IBaseStats
    {
        public IBaseStats(Player player)
        {
            Player = player;
        }

        public Player Player { get; set; }
    }

    internal class WeaponsStats : IBaseStats
    {
        public WeaponsStats(Player player)
               : base(player)
        {
        }

        public UserDataItemDto[] GetUserDataDto()
        {
            var slots = new List<UserDataItemDto>();
            for (WeaponSlot slot = 0; slot < WeaponSlot.None; slot++)
            {
                var equipped = Player.CharacterManager.CurrentCharacter.Weapons.GetItem(slot);
                slots.Add(new UserDataItemDto
                {
                    itemNumber = equipped?.ItemNumber ?? 0,
                    priceType = equipped?.PriceType ?? 0,
                    Unk2 = 0,
                    Period = 0,
                    Color = equipped?.Color ?? 0,
                    Effects = equipped?.GetItemEffectsInt() ?? new uint[0],
                    EnchantLv = 0,
                    Unk3 = 0
                });
            }
            return slots.ToArray();
        }
    }

    internal class SkillStats : IBaseStats
    {
        public SkillStats(Player player)
               : base(player)
        {
        }

        public UserDataItemDto[] GetUserDataDto()
        {
            var slots = new List<UserDataItemDto>();

            var equipped = Player.CharacterManager.CurrentCharacter.Skills.GetItem(SkillSlot.Skill);
            slots.Add(new UserDataItemDto
            {
                itemNumber = equipped?.ItemNumber ?? 0,
                priceType = equipped?.PriceType ?? 0,
                Unk2 = 0,
                Period = 0,
                Color = equipped?.Color ?? 0,
                Effects = equipped?.GetItemEffectsInt() ?? new uint[0],
                EnchantLv = 0,
                Unk3 = 0
            });

            return slots.ToArray();
        }
    }

    internal class ClotheStats : IBaseStats
    {
        public ClotheStats(Player player)
               : base(player)
        {
        }

        public UserDataItemDto[] GetUserDataDto()
        {
            var slots = new List<UserDataItemDto>();

            for (CostumeSlot slot = 0; slot < CostumeSlot.Max; slot++)
            {
                var equipped = Player.CharacterManager.CurrentCharacter.Costumes.GetItem(slot);
                slots.Add(new UserDataItemDto
                {
                    itemNumber = equipped?.ItemNumber ?? 0,
                    priceType = equipped?.PriceType ?? 0,
                    Unk2 = 0,
                    Period = 0,
                    Color = equipped?.Color ?? 0,
                    Effects = equipped?.GetItemEffectsInt() ?? new uint[0],
                    EnchantLv = 0,
                    Unk3 = 0
                });
            }

            return slots.ToArray();
        }
    }
}

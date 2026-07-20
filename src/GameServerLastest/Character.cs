using System;
using System.Collections.Generic;
using System.Linq;
using Santana;
using Santana.Database.Game;
using Santana.Network;
using Santana.Resource;

namespace Santana
{
    internal class Character
    {
        private Character _source;

        internal Character(CharacterManager characterManager, PlayerCharacterDto dto)
        {
            CharacterManager = characterManager;

            Weapons = new WeaponManager(this, dto);
            Skills = new SkillManager(this, dto);
            Costumes = new CostumeManager(this, dto);

            GameServer.Instance.ResourceCache.GetDefaultItems();

            ExistsInDatabase = true;
            Id = dto.Id;
            Slot = dto.Slot;
            Gender = (CharacterGender)dto.Gender;
        }

        internal Character(CharacterManager characterManager, byte slot, CharacterGender gender)
        {
            CharacterManager = characterManager;

            Weapons = new WeaponManager(this);
            Skills = new SkillManager(this);
            Costumes = new CostumeManager(this);

            Slot = slot;
            Gender = gender;
        }

        public Character(Character plater)
        {
            _source = plater;
        }

        public CharacterManager CharacterManager { get; }

        internal bool ExistsInDatabase { get; set; }
        internal bool NeedsToSave { get; set; }

        public int Id { get; }
        public byte Slot { get; }

        public CharacterGender Gender { get; }
        public WeaponManager Weapons { get; }
        public SkillManager Skills { get; }
        public CostumeManager Costumes { get; }

        public void Equip(PlayerItem item, byte slot)
        {
            switch (item.ItemNumber.Category)
            {
                case ItemCategory.Costume:
                    Costumes.Equip(item, (CostumeSlot)slot);
                    break;

                case ItemCategory.Weapon:
                    Weapons.Equip(item, (WeaponSlot)slot);
                    break;

                case ItemCategory.Skill:
                    Skills.Equip(item, (SkillSlot)slot);
                    break;

                case ItemCategory.Boost:
                    CharacterManager.Boosts.Equip(item, (BoostSlot)slot);
                    break;

                default:
                    throw new CharacterException("Invalid category " + item.ItemNumber.Category);
            }
        }

        public void UnEquip(ItemCategory category, byte slot, ulong itemid)
        {
            switch (category)
            {
                case ItemCategory.Costume:
                    Costumes.UnEquip((CostumeSlot)slot);
                    break;

                case ItemCategory.Weapon:
                    Weapons.UnEquip((WeaponSlot)slot);
                    break;

                case ItemCategory.Skill:
                    Skills.UnEquip((SkillSlot)slot);
                    break;

                case ItemCategory.Boost:
                    CharacterManager.Boosts.UnEquip((BoostSlot)slot, itemid);
                    break;

                default:
                    throw new CharacterException("Invalid category" + category);
            }
        }

        public bool CanEquip(PlayerItem item, byte slot)
        {
            switch (item.ItemNumber.Category)
            {
                case ItemCategory.Costume:
                    return Costumes.CanEquip(item, (CostumeSlot)slot);

                case ItemCategory.Weapon:
                    return Weapons.CanEquip(item, (WeaponSlot)slot);

                case ItemCategory.Skill:
                    return Skills.CanEquip(item, (SkillSlot)slot);

                default:
                    return false;
            }
        }

        public uint GetHP()
        {
            var bonusHp = 0U;

            var equipped = new List<PlayerItem>();
            equipped.AddRange(Costumes.GetItems());
            equipped.AddRange(Skills.GetItems());
            equipped.AddRange(Weapons.GetItems());

            foreach (var equippedItem in equipped)
            {
                if (equippedItem?.Effects == null)
                    continue;

                foreach (var effect in equippedItem.Effects.Where(x => x.Number < 1000))
                {
                    if (effect.SubCategory < 10 &&
                        effect.Sub2Category == 30)
                    {
                        bonusHp += effect.Number;
                    }
                    else
                    {
                        switch (effect)
                        {
                            case 1999300009:
                                bonusHp += 15;
                                break;
                            case 1999300010:
                                bonusHp += 20;
                                break;
                            case 1999300012:
                                bonusHp += 25;
                                break;
                            case 1999300011:
                                bonusHp += 30;
                                break;
                        }
                    }
                }
            }

            var owner = CharacterManager?.Player;
            if (owner?.CollectBookEffects != null)
            {
                foreach (var effect in owner.CollectBookEffects.Where(x => x.Number < 1000))
                {
                    if (effect.SubCategory < 10 &&
                        effect.Sub2Category == 30)
                    {
                        bonusHp += effect.Number;
                    }
                    else
                    {
                        switch (effect)
                        {
                            case 1999300009:
                                bonusHp += 15;
                                break;
                            case 1999300010:
                                bonusHp += 20;
                                break;
                            case 1999300012:
                                bonusHp += 25;
                                break;
                            case 1999300011:
                                bonusHp += 30;
                                break;
                        }
                    }
                }
            }

            return bonusHp;
        }

        public uint GetSP()
        {
            var bonusSp = 0U;

            var equipped = new List<PlayerItem>();
            equipped.AddRange(Costumes.GetItems());
            equipped.AddRange(Skills.GetItems());
            equipped.AddRange(Weapons.GetItems());

            foreach (var equippedItem in equipped)
            {
                if (equippedItem?.Effects == null)
                    continue;

                foreach (var effect in equippedItem.Effects.Where(x => x.Number > 1004 && x.Number < 1009))
                {

                    if (effect.SubCategory < 10 &&
                        effect.Sub2Category == 30)
                    {
                        bonusSp += effect.Number - 1000U;
                    }
                    else
                    {
                        switch (effect)
                        {
                            case 1999301011:
                                bonusSp += 20;
                                break;
                            case 1999301013:
                                bonusSp += 25;
                                break;
                            case 1300301012:
                                bonusSp += 40;
                                break;
                        }
                    }
                }
            }

            var owner = CharacterManager?.Player;
            if (owner?.CollectBookEffects != null)
            {
                foreach (var effect in owner.CollectBookEffects.Where(x => x.Number > 1004 && x.Number < 1009))
                {
                    if (effect.SubCategory < 10 &&
                        effect.Sub2Category == 30)
                    {
                        bonusSp += effect.Number - 1000U;
                    }
                    else
                    {
                        switch (effect)
                        {
                            case 1999301011:
                                bonusSp += 20;
                                break;
                            case 1999301013:
                                bonusSp += 25;
                                break;
                            case 1300301012:
                                bonusSp += 40;
                                break;
                        }
                    }
                }
            }

            return bonusSp;
        }
    }
}

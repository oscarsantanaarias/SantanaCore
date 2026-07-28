using System;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Threading.Tasks;
using Santana.Database.Game;
using Santana.Network.Message.Game;

namespace Santana
{
    internal class SkillManager
    {
        private readonly Character _owner;
        public readonly PlayerItem[] _items = new PlayerItem[2];
        internal readonly AsyncLock _sync = new AsyncLock();

        internal SkillManager(Character @char, PlayerCharacterDto dto)
        {
            _owner = @char;
            _items[0] = _owner.CharacterManager.Player.Inventory[(ulong)(dto.SkillId ?? 0)];
        }

        internal SkillManager(Character @char)
        {
            _owner = @char;
        }

        public void Equip(PlayerItem item, SkillSlot slot, bool allow = false)
        {
            if (item == null)
            {
                Console.WriteLine(nameof(item));
                return;
            }

            if (!CanEquip(item, slot) && !allow)
            {
                Console.WriteLine($"Equip refused - item {item.ItemNumber} is not permitted in slot {slot}");
                return;
            }

            if (slot != SkillSlot.Skill)
            {
                Console.WriteLine("Skill equip aborted, unsupported slot: " + slot);
                return;
            }

            var slotIndex = (int)slot;
            if (_items[slotIndex] != item)
            {
                _owner.NeedsToSave = true;
                _items[slotIndex] = item;
            }

            _owner.CharacterManager.Player.Session.SendAsync(new ItemUseItemAckMessage
            {
                CharacterSlot = _owner.Slot,
                ItemId = item.Id,
                Action = UseItemAction.Equip,
                EquipSlot = (byte)slot
            });
        }

        public void UnEquip(SkillSlot slot, bool allow = false)
        {
            var player = _owner.CharacterManager.Player;

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby && !allow)
            {
                Console.WriteLine("Loadout is locked while a match is running");
                return;
            }

            if (slot != SkillSlot.Skill)
            {
                Console.WriteLine("Skill unequip aborted, unsupported slot: " + slot);
                return;
            }

            var slotIndex = (int)slot;
            var removed = _items[slotIndex];
            if (removed != null)
            {
                _owner.NeedsToSave = true;
                _items[slotIndex] = null;
            }

            player.Session.SendAsync(new ItemUseItemAckMessage
            {
                CharacterSlot = _owner.Slot,
                ItemId = removed?.Id ?? 0,
                Action = UseItemAction.UnEquip,
                EquipSlot = (byte)slot
            });
        }

        public PlayerItem GetItem(SkillSlot slot)
        {
            if (slot == SkillSlot.Skill)
                return _items[(int)slot];

            Console.WriteLine("Skill lookup aborted, unsupported slot: " + slot);
            return null;
        }

        public IReadOnlyList<PlayerItem> GetItems()
        {
            return _items;
        }

        public bool CanEquip(PlayerItem item, SkillSlot slot)
        {
            if (item == null)
                return false;

            if (item.ItemNumber.Category != ItemCategory.Skill)
                return false;

            if (slot != SkillSlot.Skill)
                return false;

            var slotIndex = (int)slot;
            var player = _owner.CharacterManager.Player;

            if (_items[slotIndex] != null)
                player.CharacterManager.CurrentCharacter.Skills.UnEquip(slot);

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby)
                return false;

            foreach (var character in player.CharacterManager)
                if (character.Skills.GetItems().Any(worn => worn?.Id == item.Id))
                    return false;

            return true;
        }
    }
}

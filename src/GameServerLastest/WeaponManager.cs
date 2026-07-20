using System;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Threading.Tasks;
using Santana.Database.Game;
using Santana.Network.Message.Game;

namespace Santana
{
    internal class WeaponManager
    {
        private readonly Character _owner;
        public readonly PlayerItem[] _items = new PlayerItem[3];
        internal readonly AsyncLock _sync = new AsyncLock();

        internal WeaponManager(Character @char, PlayerCharacterDto dto)
        {
            _owner = @char;
            var inventory = _owner.CharacterManager.Player.Inventory;

            _items[0] = inventory[(ulong)(dto.Weapon1Id ?? 0)];
            _items[1] = inventory[(ulong)(dto.Weapon2Id ?? 0)];
            _items[2] = inventory[(ulong)(dto.Weapon3Id ?? 0)];
        }

        internal WeaponManager(Character @char)
        {
            _owner = @char;
        }

        public void Equip(PlayerItem item, WeaponSlot slot, bool allow = false)
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

            var slotIndex = (int)slot;
            if (slot != WeaponSlot.Weapon1 && slot != WeaponSlot.Weapon2 && slot != WeaponSlot.Weapon3)
            {
                Console.WriteLine("Weapon equip aborted, unsupported slot: " + slot);
                return;
            }

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

        public void UnEquip(WeaponSlot slot, bool allow = false)
        {
            var player = _owner.CharacterManager.Player;

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby && !allow)
            {
                Console.WriteLine("Loadout is locked while a match is running");
                return;
            }

            if (slot != WeaponSlot.Weapon1 && slot != WeaponSlot.Weapon2 && slot != WeaponSlot.Weapon3)
            {
                Console.WriteLine("Weapon unequip aborted, unsupported slot: " + slot);
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

        public PlayerItem GetItem(WeaponSlot slot)
        {
            if (slot == WeaponSlot.Weapon1 || slot == WeaponSlot.Weapon2 || slot == WeaponSlot.Weapon3)
                return _items[(int)slot];

            Console.WriteLine("Weapon lookup aborted, unsupported slot: " + slot);
            return null;
        }

        public IReadOnlyList<PlayerItem> GetItems()
        {
            return _items;
        }

        public bool CanEquip(PlayerItem item, WeaponSlot slot)
        {
            if (item == null)
                return false;

            if (item.ItemNumber.Category != ItemCategory.Weapon)
                return false;

            if (slot < WeaponSlot.Weapon1 || slot > WeaponSlot.Weapon3)
                return false;

            var slotIndex = (int)slot;

            if (_items[slotIndex] != null)
                return false;

            var player = _owner.CharacterManager.Player;

            if (_items[slotIndex] != null)
                player.CharacterManager.CurrentCharacter.Weapons.UnEquip(slot);

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby)
                return false;

            foreach (var character in player.CharacterManager)
                if (character.Weapons.GetItems().Any(worn => worn?.Id == item.Id))
                    return false;

            return true;
        }
    }
}

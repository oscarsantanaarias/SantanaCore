using System;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Threading.Tasks;
using Santana.Database.Game;
using Santana.Network.Data.Chat;
using Santana.Network.Message.Game;

namespace Santana
{
    internal class CostumeManager
    {
        private readonly Character _owner;
        private readonly PlayerItem[] _slots = new PlayerItem[8];
        internal readonly AsyncLock _sync = new AsyncLock();

        internal CostumeManager(Character @char, PlayerCharacterDto dto)
        {
            _owner = @char;
            var inventory = _owner.CharacterManager.Player.Inventory;

            _slots[0] = inventory[(ulong)(dto.HairId ?? 0)];
            _slots[1] = inventory[(ulong)(dto.FaceId ?? 0)];
            _slots[2] = inventory[(ulong)(dto.ShirtId ?? 0)];
            _slots[3] = inventory[(ulong)(dto.PantsId ?? 0)];
            _slots[4] = inventory[(ulong)(dto.GlovesId ?? 0)];
            _slots[5] = inventory[(ulong)(dto.ShoesId ?? 0)];
            _slots[6] = inventory[(ulong)(dto.AccessoryId ?? 0)];
            _slots[7] = inventory[(ulong)(dto.PetId ?? 0)];
        }

        internal CostumeManager(Character @char)
        {
            _owner = @char;
        }

        public void Equip(PlayerItem item, CostumeSlot slot, bool silent = false, bool allow = false)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            if (!CanEquip(item, slot) && !allow)
                throw new CharacterException($"Cannot equip item {item.ItemNumber} on slot {slot}");

            if (slot > CostumeSlot.Pet)
                throw new CharacterException("Invalid slot: " + (byte)slot);

            var slotIndex = (uint)slot;
            if (_slots[slotIndex] != item)
            {
                _owner.NeedsToSave = true;
                _slots[slotIndex] = item;
            }

            if (silent)
                return;

            _owner.CharacterManager.Player.Session.SendAsync(new ItemUseItemAckMessage
            {
                CharacterSlot = _owner.Slot,
                ItemId = item?.Id ?? 0,
                Action = UseItemAction.Equip,
                EquipSlot = (byte)slot
            });
        }

        public void UnEquip(CostumeSlot slot, bool allow = false)
        {
            var player = _owner.CharacterManager.Player;

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby && !allow)
                throw new CharacterException("Can't change items while playing");

            if (slot > CostumeSlot.Pet)
                throw new CharacterException("Invalid slot: " + slot);

            var slotIndex = (uint)slot;
            var removed = _slots[slotIndex];
            if (removed != null)
            {
                _owner.NeedsToSave = true;
                _slots[slotIndex] = null;
            }

            player.Session.SendAsync(new ItemUseItemAckMessage
            {
                CharacterSlot = _owner.Slot,
                ItemId = removed?.Id ?? 0,
                Action = UseItemAction.UnEquip,
                EquipSlot = (byte)slot
            });
        }

        public void RefreshEsperSkill()
        {
            if (_owner.CharacterManager.CurrentCharacter != _owner)
                return;
            var skill = GetEsperSkill();
            if (skill == Resource.EsperSkillType.None)
                _owner.CharacterManager.Player.Session.SendAsync(new EspherChipLv5Message());
            else
                _owner.CharacterManager.Player.Session.SendAsync(new EspherChipLv5Message((int)skill));
        }

        public PlayerItem GetItem(CostumeSlot slot)
        {
            if (slot > CostumeSlot.Pet)
                throw new CharacterException("Invalid slot: " + slot);

            return _slots[(uint)slot];
        }

        public IReadOnlyList<PlayerItem> GetItems()
        {
            return _slots;
        }

        public Resource.EsperSkillType GetEsperSkill()
        {
            var table = Network.GameServer.Instance.ResourceCache.GetEsperEnchant();
            var shirt = GetItem(CostumeSlot.Shirt);
            if (shirt == null || shirt.EsperChip == 0)
                return Resource.EsperSkillType.None;
            if (!table.TryGetValue(shirt.EsperChip, out var reference) || reference.Level != 5)
                return Resource.EsperSkillType.None;

            var matching = 0;
            foreach (var piece in _slots)
            {
                if (piece == null || piece.EsperChip == 0)
                    continue;
                if (!table.TryGetValue(piece.EsperChip, out var entry))
                    continue;
                if (entry.Level == reference.Level && entry.Type == reference.Type)
                    matching++;
            }

            return matching > 3 ? reference.Type : Resource.EsperSkillType.None;
        }

        public bool CanEquip(PlayerItem item, CostumeSlot slot)
        {
            if (item == null)
                return false;

            if (item.ItemNumber.Category != ItemCategory.Costume)
                return false;

            if (slot > CostumeSlot.Pet || slot < CostumeSlot.Hair)
                return false;

            var player = _owner.CharacterManager.Player;

            if (_slots[(int)slot] != null)
                player.CharacterManager.CurrentCharacter.Costumes.UnEquip(slot);

            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby)
                return false;

            foreach (var character in player.CharacterManager)
                if (character.Costumes.GetItems().Any(worn => worn?.Id == item.Id))
                    return false;

            return true;
        }
    }
}

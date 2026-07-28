using Dapper.FastCrud;
using ExpressMapper.Extensions;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network.Data.Chat;
using Santana.Network.Data.GameRule;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using Santana.Network.Message.GameRule;
using Santana.Network.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
namespace Santana
{
    internal class BoostManager
    {
        Player player;
        private readonly PlayerItem[] _items = new PlayerItem[7];
        internal BoostManager(Player plr, PlayerDto db)
        {
            player = plr;
        }
        internal BoostManager(Player plr)
        {
            player = plr;
            using (var connection = GameDatabase.Open())
            {
                var boosterRow = DbUtil.Find<PlayerBoostersDto>(connection, statement => statement
                   .Where($"{nameof(PlayerBoostersDto.PlayerId):C} = @{nameof(plr.Account.Id)}")
                   .WithParameters(new { plr.Account.Id })).FirstOrDefault();
                if (boosterRow == null)
                    return;
                _items[0] = plr.Inventory[(ulong)(boosterRow.PEN ?? 0)];
                _items[1] = plr.Inventory[(ulong)(boosterRow.EXP ?? 0)];
                _items[2] = plr.Inventory[(ulong)(boosterRow.MP ?? 0)];
                _items[3] = plr.Inventory[(ulong)(boosterRow.UNIQUE ?? 0)];
                _items[4] = plr.Inventory[(ulong)(boosterRow.NameTag ?? 0)];
                _items[5] = plr.Inventory[(ulong)(boosterRow.NameTag2 ?? 0)];
                _items[6] = plr.Inventory[(ulong)(boosterRow.NameTag3 ?? 0)];
            }
        }
        internal static uint ResolveVisibleNameTagId(PlayerItem item)
        {
            if (item == null || item.ItemNumber == null)
                return 0;
            var numberText = item.ItemNumber.ToString();
            if (string.IsNullOrWhiteSpace(numberText) || numberText.Length < 7)
                return 0;
            if (!uint.TryParse("400000" + numberText.Substring(3, 4), out var tagId))
                return 0;
            return tagId - 7;
        }
        internal static uint ResolveDisplayNameTagId(uint rawTagId)
        {
            if (rawTagId == 0)
                return 0;
            if (rawTagId >= 4000000000)
                return rawTagId % 1000;
            return rawTagId;
        }
        private void SendNameTag(uint TagId, bool IsActive)
        {
            player?.SendAsync(new CollectBookInvenEffectInfoAckMessage
            {
                Unk = 1,
                active = (byte)(IsActive ? 1 : 0),
                Unk3 = 0,
                Unk4 = 0,
                nametagid = IsActive ? TagId : 0,
                Unk5 = 0,
                Unk6 = 0,
                days = "DAYS",
                nametag = "NAMETAGS",
                zero = "00000000000000",
                zero2 = "00000000000000",
                zero3 = "00000000000000"
            });
        }
        public void PlayerNameTag()
        {
            var item = GetItem(BoostSlot.NameTag);
            if (item == null)
                player.NameTag = 0;
            else
                player.NameTag = ResolveVisibleNameTagId(item);
            if (player.CollectBookNameTag > 0)
                player.NameTag = player.CollectBookNameTag;
            SendNameTag(player.NameTag, player.NameTag > 0);
            if (player.Room != null)
            {
                player.Room.Broadcast(new RoomEnterPlayerForBookNameTagsAckMessage
                {
                    AccountId = player.Account.Id,
                    Team = player.RoomInfo.Team.Team,
                    PlayerGameMode = player.RoomInfo.Mode,
                    Exp = player.TotalExperience,
                    Nickname = player.Account.Nickname,
                    Unk1 = player.NameTag,
                    Unk2 = (byte)(player.NameTag > 0 ? 1 : 0)
                });
                player.Room.Broadcast(new RoomEnterPlayerInfoListForNameTagAckMessage(player?.Room.Players.Values
                    .Select(player => new NameTagDto(player.Account.Id, player.NameTag)).ToArray()));
                player.Room.Broadcast(new Chennel_PlayerNameTagList_AckMessage(player?.Channel.Players.Values
                     .Select(p => p.Map<Player, PlayerNameTagInfoDto>()).ToArray()));
            }
            if (player.Channel != null)
                player.Channel.Broadcast(new Chennel_PlayerNameTagList_AckMessage(player?.Channel.Players.Values
                     .Select(p => p.Map<Player, PlayerNameTagInfoDto>()).ToArray()));
        }
        private void NameTagEquip(PlayerItem item, bool IsEquip)
        {
            var previousTag = player.NameTag;
            player.NameTag = ResolveVisibleNameTagId(item);
            using (var connection = GameDatabase.Open())
            {
                var boosterRow = DbUtil.Find<PlayerBoostersDto>(connection, statement => statement
                   .Where($"{nameof(PlayerBoostersDto.PlayerId):C} = @{nameof(player.Account.Id)}")
                   .WithParameters(new { player.Account.Id })).FirstOrDefault();
                if (boosterRow == null)
                    return;
                if (IsEquip)
                    boosterRow.NameTag = (int)item?.Id;
                else
                    boosterRow.NameTag = null;
                DbUtil.Update(connection, boosterRow);
            }
            player.KickToServerSelectionForNameTagChange(previousTag, player.NameTag);
        }
        public void Equip(PlayerItem item, BoostSlot slot, bool silent = false)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));
            var slotIndex = BoostType(item.ItemNumber);
            if (slotIndex < (byte)BoostSlot.NameTag)
            {
                if (_items[slotIndex] != null)
                    UnEquip((BoostSlot)slotIndex, _items[slotIndex].ItemNumber, true);
            }
            if (slotIndex >= (byte)BoostSlot.NameTag)
            {
                slotIndex = (byte)BoostSlot.NameTag;
                if (_items[slotIndex] != null)
                    UnEquip(BoostSlot.NameTag, _items[slotIndex].ItemNumber, true);
                NameTagEquip(item, true);
            }
            if (!CanEquip(item, (BoostSlot)slotIndex))
                throw new CharacterException($"Cannot equip item {item.ItemNumber} on slot {(BoostSlot)slotIndex}");
            if (slotIndex > (byte)BoostSlot.Max)
                throw new CharacterException("Invalid slot: " + (BoostSlot)slotIndex);
            if (_items[slotIndex] != item)
            {
                player.NeedsToSave = true;
                _items[slotIndex] = item;
            }
            if (!silent)
            {
                player.SendAsync(new ItemUseItemAckMessage
                {
                    CharacterSlot = 0,
                    ItemId = (item?.Id ?? 0),
                    Action = UseItemAction.Equip,
                    EquipSlot = slotIndex
                });
                if (slotIndex >= (byte)BoostSlot.NameTag && slotIndex < (byte)BoostSlot.Max)
                    PlayerNameTag();
            }
        }
        public void UnEquip(BoostSlot slot, ulong itemid, bool force = false)
        {
            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby)
                throw new CharacterException("Can't change items while playing");
            var slotIndex = BoostType(itemid);
            if (slotIndex >= (byte)BoostSlot.NameTag && slotIndex < (byte)BoostSlot.Max)
            {
                using (var connection = GameDatabase.Open())
                {
                    var boosterRow = DbUtil.Find<PlayerBoostersDto>(connection, statement => statement
                        .Where($"{nameof(PlayerBoostersDto.PlayerId):C} = @{nameof(player.Account.Id)}")
                        .WithParameters(new { player.Account.Id })).FirstOrDefault();
                    var inventoryItem = player.Inventory.FirstOrDefault(x => x.ItemNumber == itemid);
                    if ((int)inventoryItem.Id == boosterRow.NameTag)
                        slotIndex = (byte)BoostSlot.NameTag;
                    if (!force)
                        NameTagEquip(inventoryItem, false);
                }
            }
            if (slotIndex > (byte)BoostSlot.Max)
                throw new CharacterException("Invalid slot: " + slot);
            var equipped = _items[slotIndex];
            if (equipped != null)
            {
                player.NeedsToSave = true;
                _items[slotIndex] = null;
            }
            player.Session.SendAsync(new ItemUseItemAckMessage
            {
                CharacterSlot = 0,
                ItemId = (equipped?.Id ?? 0),
                Action = UseItemAction.UnEquip,
                EquipSlot = slotIndex
            });
            if (slotIndex >= (byte)BoostSlot.NameTag && slotIndex < (byte)BoostSlot.Max)
                PlayerNameTag();
        }
        public byte BoostType(ulong item)
        {
            if (item > 5000000 && item < 5000011)
                return 0;
            if (item > 5020000 && item < 5020004)
                return 1;
            if (item > 5030000 && item < 5030003)
                return 2;
            if (item > 5040000 && item < 5040008)
                return 3;
            if (item > 5040007 && item < 5040050)
                return 4;
            return 8;
        }
        public int GetUniqueBoosterRate()
        {
            var uniqueBoost = _items[(int)BoostSlot.UNIUQE];
            if (uniqueBoost == null)
                return 0;
            if (uniqueBoost.ItemNumber == 5040001)
                return 1;
            if (uniqueBoost.ItemNumber == 5040002)
                return 2;
            if (uniqueBoost.ItemNumber == 5040003)
                return 3;
            if (uniqueBoost.ItemNumber == 5040004)
                return 4;
            if (uniqueBoost.ItemNumber == 5040005)
                return 5;
            if (uniqueBoost.ItemNumber == 5040006)
                return 5040006;
            if (uniqueBoost.ItemNumber == 5040007)
                return 5040007;
            return 0;
        }
        public float GetExpRate()
        {
            var rate = player.CollectBookExpRate;
            if (_items[(int)BoostSlot.EXP] == null)
                return rate;
            if (_items[(int)BoostSlot.EXP].ItemNumber == 5020001)
                rate += 0.30f;
            else if (_items[(int)BoostSlot.EXP].ItemNumber == 5020002)
                rate += 0.20f;
            else if (_items[(int)BoostSlot.EXP].ItemNumber == 5020003)
                rate += 0.10f;
            return rate;
        }
        public float GetPenRate()
        {
            var rate = player.CollectBookPenRate;
            if (_items[(int)BoostSlot.PEN] == null)
                return rate;
            if (_items[(int)BoostSlot.PEN].ItemNumber == 5000001)
                rate += 0.5f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000002)
                rate += 1f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000003)
                rate += 0.10f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000004)
                rate += 0.20f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000005)
                rate += 0.30f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000006)
                rate += 0.40f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000007)
                rate += 0.60f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000008)
                rate += 0.70f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000009)
                rate += 0.80f;
            else if (_items[(int)BoostSlot.PEN].ItemNumber == 5000010)
                rate += 0.90f;
            return rate;
        }
        public PlayerItem GetItem(BoostSlot slot)
        {
            if (slot > BoostSlot.Max)
                throw new CharacterException("Invalid slot: " + slot);
            return _items[(uint)slot];
        }
        public IReadOnlyList<PlayerItem> GetItems()
        {
            return _items;
        }
        public bool CanEquip(PlayerItem item, BoostSlot slot)
        {
            if (item == null)
                return false;
            if (item.ItemNumber.Category != ItemCategory.Boost)
                return false;
            if (slot > BoostSlot.Max || slot < BoostSlot.PEN)
                return false;
            if (_items[(int)slot] != null)
                return false;
            if (player.Room != null && player.RoomInfo.State != PlayerState.Lobby)
                return false;
            foreach (var character in player.CharacterManager)
                if (character.CharacterManager.Boosts.GetItems().Any(equipped => equipped?.Id == item.Id))
                    return false;
            return true;
        }
        internal void Save(IDbConnection db)
        {
            var boosterRow = DbUtil.Find<PlayerBoostersDto>(db, statement => statement
               .Where($"{nameof(PlayerBoostersDto.PlayerId):C} = @{nameof(player.Account.Id)}")
               .WithParameters(new { player.Account.Id })).FirstOrDefault();
            if (boosterRow == null)
            {
                var newRow = new PlayerBoostersDto
                {
                    PlayerId = player.Account.Id,
                };
                SetDtoItems(newRow);
                DbUtil.Insert(db, newRow);
                player.NeedsToSave = true;
            }
            else
            {
                if (player.NeedsToSave)
                    return;
                SetDtoItems(boosterRow);
                DbUtil.Update(db, boosterRow);
                player.NeedsToSave = false;
            }
        }
        private void SetDtoItems(PlayerBoostersDto row)
        {
            for (var slot = BoostSlot.PEN; slot <= BoostSlot.UNIUQE; slot++)
            {
                var equipped = GetItem(slot);
                var equippedId = equipped != null ? (int?)equipped.Id : null;
                switch (slot)
                {
                    case BoostSlot.PEN:
                        row.PEN = equippedId;
                        break;
                    case BoostSlot.EXP:
                        row.EXP = equippedId;
                        break;
                    case BoostSlot.MP:
                        row.MP = equippedId;
                        break;
                    case BoostSlot.UNIUQE:
                        row.UNIQUE = equippedId;
                        break;
                    case BoostSlot.NameTag:
                        row.NameTag = equippedId;
                        break;
                    case BoostSlot.NameTag2:
                        row.NameTag2 = equippedId;
                        break;
                    case BoostSlot.NameTag3:
                        row.NameTag3 = equippedId;
                        break;
                }
            }
        }
    }
}

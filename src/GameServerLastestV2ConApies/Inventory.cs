using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using SantanaLib.Collections.Concurrent;
using Dapper.FastCrud;
using ExpressMapper.Extensions;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Data.Game;
using Santana.Network.Message.Game;
using Santana.Resource;
using Santana.Shop;
namespace Santana
{
    internal class Inventory : IReadOnlyCollection<PlayerItem>
    {
        private readonly ConcurrentDictionary<ulong, PlayerItem> _items = new ConcurrentDictionary<ulong, PlayerItem>();
        private readonly ConcurrentStack<PlayerItem> _itemsToDelete = new ConcurrentStack<PlayerItem>();
        internal Inventory(Player plr, PlayerDto dto)
        {
            Player = plr;
            foreach (var loaded in dto.Items.Select(row => new PlayerItem(this, row)))
            {
                if (!loaded.IsInvalid)
                    _items.TryAdd(loaded.Id, loaded);
                else
                    _itemsToDelete.Push(loaded);
            }
        }
        public Player Player { get; }
        public PlayerItem this[ulong id] => GetItem(id);
        public int Count => _items.Count;
        public IEnumerator<PlayerItem> GetEnumerator()
        {
            return _items.Values.GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        public PlayerItem GetItem(ulong id)
        {
            _items.TryGetValue(id, out var found);
            return found;
        }
        public PlayerItem GetItemByShopInfoId(uint id)
        {
            try
            {
                var item = _items.Values.Where(item_ => item_.GetShopItemInfo().Id == id).ToList();
                if (item.Count < 1)
                    return null;
                return item.LastOrDefault();
            }
            catch
            {
                return null;
            }
        }
        public PlayerItem GetItemByItemId(ItemNumber itemNumber)
        {
            try
            {
                PlayerItem lastMatch = null;
                foreach (var stored in _items.Values)
                {
                    if (stored.ItemNumber == itemNumber)
                        lastMatch = stored;
                }
                return lastMatch;
            }
            catch
            {
                return null;
            }
        }
        public PlayerItem Create(ItemNumber itemNumber,
            ushort period, byte color, EffectNumber[] effects, uint count, int effectlvl = 0, bool deffeffect = false)
        {
            var shop = GameServer.Instance.ResourceCache.GetShop();
            var shopItemInfo = shop.GetFirstItemInfo(itemNumber);
            if (shopItemInfo == null)
                throw new ArgumentException($"Item not found : {itemNumber.Id}");
            var itemEffects = new List<EffectNumber>();
            if (!deffeffect)
            {
                foreach (var effect in shopItemInfo.EffectGroup.Effects)
                {
                    itemEffects.Add(effect.Effect);
                }
            }
            else
            {
                itemEffects.AddRange(effects.ToArray());
            }
            var priceType = shopItemInfo.PriceGroup.PriceType;
            var periodType = shopItemInfo.PriceGroup.Prices.FirstOrDefault().PeriodType;
            var periodNr = shopItemInfo.PriceGroup.Prices.FirstOrDefault().Period;
            return Create(itemNumber, priceType, periodType, periodNr, color, itemEffects.ToArray(), count, effectlvl);
        }
        public PlayerItem Create(ItemNumber itemNumber, ItemPriceType priceType, ItemPeriodType periodType,
            ushort period, byte color, EffectNumber[] effects, uint count, int effectlvl = 0)
        {
            var shop = GameServer.Instance.ResourceCache.GetShop();
            var shopItemInfo = shop.GetItemInfo(itemNumber, priceType);
            if (shopItemInfo == null)
                throw new ArgumentException($"Item not found : {itemNumber.Id}");
            var price = shopItemInfo.PriceGroup.GetPrice(periodType, period);
            if (price == null)
                throw new ArgumentException($"Price not found : {priceType}");
            return Create(shopItemInfo, price, color, effects, count, effectlvl);
        }
        public PlayerItem CreateSilent(ItemNumber itemNumber,
            ushort period, byte color, uint count)
        {
            var shop = GameServer.Instance.ResourceCache.GetShop();
            var shopItemInfo = shop.GetFirstItemInfo(itemNumber);
            if (shopItemInfo == null)
                throw new ArgumentException($"Item not found : {itemNumber.Id}");
            if (shopItemInfo == null)
                throw new ArgumentException($"Item not found : {itemNumber.Id}");
            var price = shopItemInfo.PriceGroup.Prices.FirstOrDefault();
            if (price == null)
                throw new ArgumentException($"Item has no price");
            var effects = shopItemInfo.EffectGroup.Effects.Select(x => (EffectNumber)x.Effect).ToArray();
            return CreateSilent(shopItemInfo, price, color, effects, count);
        }
        public PlayerItem Create(ShopItemInfo shopItemInfo, ShopPrice price, byte color, EffectNumber[] effects,
            uint count, int effectlvl = 0)
        {
            if (effects.Length == 0)
                effects = new EffectNumber[] { 0 };
            var item = new PlayerItem(this, shopItemInfo, price, color, effects, DateTimeOffset.Now, effectlvl, count);
            _items.TryAdd(item.Id, item);
            Player.Session.SendAsync(
                new ItemUpdateInventoryAckMessage(InventoryAction.Add, item.Map<PlayerItem, ItemDto>()));
            return item;
        }
        public PlayerItem CreateSilent(ShopItemInfo shopItemInfo, ShopPrice price, byte color, EffectNumber[] effects,
            uint count)
        {
            if (effects.Length == 0)
                effects = new EffectNumber[] { 0 };
            var item = new PlayerItem(this, shopItemInfo, price, color, effects, DateTimeOffset.Now, count);
            _items.TryAdd(item.Id, item);
            return item;
        }
        public void Remove(PlayerItem item)
        {
            Remove(item.Id);
        }
        public void RemoveOrDecreaseDays(PlayerItem item, ushort days)
        {
            if (item.PeriodType == ItemPeriodType.Days)
            {
                item.DaysLeft-= days;
                if (item.DaysLeft <= 0)
                {
                    Remove(item.Id);
                }
                else
                {
                    Network.Services.ShopService.UpdateItemInDB(Player, item);
                    Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                        item.Map<PlayerItem, ItemDto>()));
                }
            }
            else
            {
                Remove(item.Id);
            }
        }
        public void RemoveOrDecrease(PlayerItem item)
        {
            if (item.PeriodType == ItemPeriodType.Units)
            {
                item.Count--;
                if (item.Count <= 0)
                {
                    Remove(item.Id);
                }
                else
                {
                    Network.Services.ShopService.UpdateItemInDB(Player, item);
                    Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                        item.Map<PlayerItem, ItemDto>()));
                }
            }
            else
            {
                Remove(item.Id);
            }
        }
        public void RemoveOrDecrease(ulong id)
        {
            var target = GetItem(id);
            if (target == null)
                return;
            if (target.PeriodType != ItemPeriodType.Units)
            {
                Remove(target.Id);
                return;
            }
            target.Count--;
            if (target.Count <= 0)
            {
                Remove(target.Id);
                return;
            }
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                target.Map<PlayerItem, ItemDto>()));
        }
        public void RemoveOrDecreaseCount(ulong id, uint Count)
        {
            var target = GetItem(id);
            if (target == null)
                return;
            if (target.PeriodType != ItemPeriodType.Units)
            {
                Remove(target.Id);
                return;
            }
            if (Count > target.Count)
                return;
            target.Count -= Count;
            if (target.Count <= 0)
            {
                Remove(target.Id);
                return;
            }
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                target.Map<PlayerItem, ItemDto>()));
        }
        public void RemoveOrDecreaseCount(PlayerItem item, uint Count)
        {
            if (item.PeriodType != ItemPeriodType.Units)
            {
                Remove(item.Id);
                return;
            }
            item.Count -= Count;
            if (item.Count <= 0)
            {
                Remove(item.Id);
                return;
            }
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                item.Map<PlayerItem, ItemDto>()));
        }
        public void CreateDays(ItemNumber item, ushort Days)
        {
            var stacked = Player.Inventory.FirstOrDefault(x => x.ItemNumber == item);
            if (stacked == null)
            {
                Player.Inventory.Create(item, Days, 0, new EffectNumber[] { 0 }, 1);
                return;
            }
            stacked.DaysLeft += Days;
            stacked.NeedsToSave = true;
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                    stacked.Map<PlayerItem, ItemDto>()));
        }
        public void CreateDays(ItemNumber item, ushort Days, byte Color)
        {
            var stacked = Player.Inventory.FirstOrDefault(x => x.ItemNumber == item && x.Color == Color);
            if (stacked == null)
            {
                Player.Inventory.Create(item, Days, Color, new EffectNumber[] { 0 }, 1);
                return;
            }
            stacked.DaysLeft += Days;
            stacked.NeedsToSave = true;
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                    stacked.Map<PlayerItem, ItemDto>()));
        }
        public void CreateUnits(ItemNumber item, uint Units)
        {
            var stacked = Player.Inventory.FirstOrDefault(x => x.ItemNumber == item);
            if (stacked == null)
            {
                Player.Inventory.Create(item, 1, 0, new EffectNumber[] { 0 }, Units);
                return;
            }
            stacked.Count += Units;
            stacked.NeedsToSave = true;
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                    stacked.Map<PlayerItem, ItemDto>()));
        }
        public void CreateUnits(ItemNumber item, byte color, EffectNumber[] effects, uint Units)
        {
            var stacked = Player.Inventory.FirstOrDefault(x => x.ItemNumber == item && x.Color == color);
            if (stacked == null)
            {
                if (effects.Length == 0)
                    effects = new EffectNumber[] { 0 };
                Player.Inventory.Create(item, 1, color, effects, Units);
                return;
            }
            stacked.Count += Units;
            stacked.NeedsToSave = true;
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                    stacked.Map<PlayerItem, ItemDto>()));
        }
        public void Update(ulong id)
        {
            var target = GetItem(id);
            if (target == null)
                throw new ArgumentException($"Item {id} not found", nameof(id));
            target.EnchantLvl = 0;
            target.NeedsToSave = true;
            Player.SendAsync(new ItemUpdateInventoryAckMessage(InventoryAction.Update,
                  target.Map<PlayerItem, ItemDto>()));
        }
        public void Remove(ulong id)
        {
            var target = GetItem(id);
            if (target == null)
                throw new ArgumentException($"Item {id} not found", nameof(id));
            _items.Remove(target.Id);
            if (target.ExistsInDatabase)
            {
                using (var db = GameDatabase.Open())
                {
                    DbUtil.BulkDelete<PlayerItemDto>(db, statement => statement
                       .Where($"{nameof(PlayerItemDto.Id):C} IN ({target.Id})"));
                }
            }
            Player.Session.SendAsync(new ItemInventroyDeleteAckMessage(target.Id));
        }
        public void RemoveInvalid(PlayerItem item)
        {
            _items.Remove(item.Id);
            if (item.ExistsInDatabase)
                _itemsToDelete.Push(item);
            Player.AP = (uint)item.GetShopPrice().Price;
            Player.Session.SendAsync(new ItemInventroyDeleteAckMessage(item.Id));
        }
        internal void Save(IDbConnection db)
        {
            try
            {
                if (Player.Room == null)
                {
                    var ExpireItems = (from it in _items
                                       where it.Value.ExpireDate == 0
                                       select it.Value).ToList();
                    foreach (var it in ExpireItems)
                        Remove(it);
                }
                if (!_itemsToDelete.IsEmpty)
                {
                    var idsToRemove = new StringBuilder();
                    var firstRun = true;
                    PlayerItem itemToDelete;
                    while (_itemsToDelete.TryPop(out itemToDelete))
                    {
                        if (firstRun)
                            firstRun = false;
                        else
                            idsToRemove.Append(',');
                        idsToRemove.Append(itemToDelete.Id);
                    }
                    DbUtil.BulkDelete<PlayerItemDto>(db, statement => statement
                        .Where($"{nameof(PlayerItemDto.Id):C} IN ({idsToRemove})"));
                }
                foreach (var item in _items.Values)
                {
                    if (item == null)
                        continue;
                    var rawEffects = item.Effects.ToList();
                    var dtoEffects = "";
                    try
                    {
                        dtoEffects = string.Join(",", rawEffects);
                    }
                    catch
                    {
                        dtoEffects = "0";
                    }
                    if (!item.ExistsInDatabase)
                    {
                        DbUtil.Insert(db, new PlayerItemDto
                        {
                            Id = (int)item.Id,
                            PlayerId = (int)Player.Account.Id,
                            ShopItemInfoId = item.GetShopItemInfo().Id,
                            ShopPriceId = item.GetShopItemInfo().PriceGroup.GetPrice(item.PeriodType, item.Period).Id,
                            DaysLeft = item.Period,
                            Period = item.Period,
                            Effects = dtoEffects,
                            Color = item.Color,
                            PurchaseDate = item.PurchaseDate.ToUnixTimeSeconds(),
                            Durability = item.Durability,
                            Count = (int)item.Count,
                            EnchantMP = item.EnchantMP,
                            EnchantLvl = item.EnchantLvl
                        });
                        item.ExistsInDatabase = true;
                        item.NeedsToSave = true;
                    }
                    else
                    {
                        if (!item.NeedsToSave)
                            continue;
                        DbUtil.Update(db, new PlayerItemDto
                        {
                            Id = (int)item.Id,
                            PlayerId = (int)Player.Account.Id,
                            ShopItemInfoId = item.GetShopItemInfo().Id,
                            ShopPriceId = item.GetShopPrice().Id,
                            Period = item.Period,
                            DaysLeft = item.DaysLeft,
                            Effects = dtoEffects,
                            Color = item.Color,
                            PurchaseDate = item.PurchaseDate.ToUnixTimeSeconds(),
                            Durability = item.Durability,
                            Count = (int)item.Count,
                            EnchantMP = item.EnchantMP,
                            EnchantLvl = item.EnchantLvl
                        });
                        item.NeedsToSave = false;
                    }
                }
            }
            catch (Exception ex) { }
        }
        public bool Contains(ulong id)
        {
            return _items.ContainsKey(id);
        }
    }
}

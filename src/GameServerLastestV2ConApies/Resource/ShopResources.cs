using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SantanaLib.IO;
using Dapper.FastCrud;
using Santana.Database.Game;
using Santana.Shop;
using Santana.ShopS4;

namespace Santana.Resource
{
    internal class ShopResources
    {
        private Dictionary<int, ShopEffectGroup> _effectGroups;
        private Dictionary<ItemNumber, ShopItem> _catalog;
        private Dictionary<int, ShopPriceGroup> _priceGroups;

        public IReadOnlyDictionary<ItemNumber, ShopItem> Items => _catalog;

        public IReadOnlyDictionary<int, ShopEffectGroup> Effects => _effectGroups;

        public IReadOnlyDictionary<int, ShopPriceGroup> Prices => _priceGroups;

        public byte[] ShopPrices = Array.Empty<byte>();
        public byte[] ShopEffects = Array.Empty<byte>();
        public byte[] ShopItems = Array.Empty<byte>();
        public byte[] ShopUniqueItems = Array.Empty<byte>();

        public string Version { get; private set; }

        public void Load()
        {
            using (var db = GameDatabase.Open())
            {
                _effectGroups = new Dictionary<int, ShopEffectGroup>();
                foreach (var effectDto in DbUtil.Find<ShopEffectGroupDto>(db, statement => statement
                    .Include<ShopEffectDto>(join => join.LeftOuterJoin())))
                {
                    var group = new ShopEffectGroup(effectDto);
                    _effectGroups.Add(group.Id, group);
                }

                _priceGroups = new Dictionary<int, ShopPriceGroup>();
                foreach (var priceDto in DbUtil.Find<ShopPriceGroupDto>(db, statement => statement
                    .Include<ShopPriceDto>(join => join.LeftOuterJoin())))
                {
                    var group = new ShopPriceGroup(priceDto);
                    _priceGroups.Add(group.Id, group);
                }

                _catalog = new Dictionary<ItemNumber, ShopItem>();
                foreach (var itemDto in DbUtil.Find<ShopItemDto>(db, statement => statement
                    .Include<ShopItemInfoDto>(join => join.LeftOuterJoin())))
                {
                    var item = new ShopItem(itemDto, this);
                    _catalog.Add(item.ItemNumber, item);
                }

                using (var writer = new BinaryWriter(new MemoryStream()))
                {
                    writer.Serialize(Prices.Values.ToArray());
                    ShopPrices = writer.ToArray();
                }

                using (var writer = new BinaryWriter(new MemoryStream()))
                {
                    writer.Serialize(Effects.Values.ToArray());
                    ShopEffects = writer.ToArray();
                }

                using (var writer = new BinaryWriter(new MemoryStream()))
                {
                    writer.Serialize(Items.Values.ToArray());
                    ShopItems = writer.ToArray();
                }

                using (var writer = new BinaryWriter(new MemoryStream()))
                {
                    writer.SerializeUniqueItems(Items.Values.ToArray());
                    ShopUniqueItems = writer.ToArray();
                }

                Version = DbUtil.Find<ShopVersionDto>(db).First().Version;

                NewShopS4Generator.GenerateIfVersionChanged(
                    Version,
                    Prices.Values.ToArray(),
                    Effects.Values.ToArray(),
                    Items.Values.ToArray());
            }
        }

        public void Clear()
        {
            _catalog.Clear();
            _effectGroups.Clear();
            _priceGroups.Clear();
            ShopPrices = Array.Empty<byte>();
            ShopEffects = Array.Empty<byte>();
            ShopItems = Array.Empty<byte>();
            ShopUniqueItems = Array.Empty<byte>();
            Version = "";
        }

        public ShopItem GetItem(ItemNumber itemNumber)
        {
            foreach (var entry in _catalog)
                if (entry.Key.Id == itemNumber.Id)
                    return entry.Value;

            return null;
        }

        public ShopItemInfo GetItemInfo(ItemNumber itemNumber, ItemPriceType priceType)
        {
            var item = GetItem(itemNumber);
            return item?.GetItemInfo(priceType);
        }

        public ShopItemInfo GetItemInfo(ItemNumber itemNumber)
        {
            var item = GetItem(itemNumber);
            return item?.GetItemInfo(itemNumber);
        }

        public ShopEffect[] GetItemEffect(ItemNumber itemNumber)
        {
            var item = GetItem(itemNumber);
            return item.ItemInfos.FirstOrDefault().EffectGroup.Effects.ToArray();
        }

        public ShopItemInfo GetFirstItemInfo(ItemNumber itemNumber)
        {
            var firstPrice = GetFirstPrice(itemNumber);
            var item = GetItem(itemNumber);
            return item?.GetItemInfo(firstPrice);
        }

        public ShopItemInfo GetItemInfo(PlayerItem item)
        {
            return GetItemInfo(item.ItemNumber, item.PriceType);
        }

        public ShopPrice GetPrice(ItemNumber itemNumber, ItemPriceType priceType, ItemPeriodType periodType,
            ushort period)
        {
            var itemInfo = GetItemInfo(itemNumber, priceType);
            return itemInfo?.PriceGroup.GetPrice(periodType, period);
        }

        public ItemPriceType GetFirstPrice(ItemNumber itemNumber)
        {
            var item = GetItem(itemNumber);

            foreach (var info in item.ItemInfos)
                if (info.PriceGroup.PriceType != ItemPriceType.None)
                    return info.PriceGroup.PriceType;

            return ItemPriceType.None;
        }

        public ShopPrice GetPrice(ItemNumber itemNumber)
        {
            var itemInfo = GetItemInfo(itemNumber);
            return itemInfo?.PriceGroup.GetPrice(itemInfo.PriceGroup.Id);
        }

        public ShopPrice GetPrice(PlayerItem item)
        {
            return GetPrice(item.ItemNumber, item.PriceType, item.PeriodType, item.Period);
        }
    }
}

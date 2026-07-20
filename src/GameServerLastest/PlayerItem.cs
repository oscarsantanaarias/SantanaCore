using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ExpressMapper.Extensions;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Data.Game;
using Santana.Network.Message.Game;
using Santana.Resource;
using Santana.Shop;
namespace Santana
{
    internal class PlayerItem
    {
        internal bool IsInvalid = false;
        private uint _stackCount;
        private int _durabilityPoints = 2400;
        private uint _enchantMana;
        private int _enchantLevel;
        internal PlayerItem(Inventory inventory, PlayerItemDto dto)
        {
            if (dto == null)
                return;
            var shop = GameServer.Instance.ResourceCache.GetShop();
            ExistsInDatabase = true;
            Inventory = inventory;
            Id = (ulong)dto.Id;
            var matchedGroup = shop.Items.Values.FirstOrDefault(group => group.GetItemInfo(dto.ShopItemInfoId) != null);
            if (matchedGroup == null)
            {
                IsInvalid = true;
                return;
            }
            ItemNumber = matchedGroup.ItemNumber;
            var matchedPriceGroup = shop.Prices.Values.FirstOrDefault(group => group.GetPrice(dto.ShopPriceId) != null);
            if (matchedPriceGroup == null)
            {
                IsInvalid = true;
                return;
            }
            var resolvedPrice = matchedPriceGroup.GetPrice(dto.ShopPriceId);
            PriceType = matchedPriceGroup.PriceType;
            PeriodType = resolvedPrice.PeriodType;
            Period = resolvedPrice.Period;
            DaysLeft = (ushort)dto.DaysLeft;
            Color = dto.Color;
            var parsedEffects = new List<EffectNumber>();
            if (string.IsNullOrEmpty(dto.Effects) || string.IsNullOrWhiteSpace(dto.Effects))
            {
                matchedGroup.ItemInfos.FirstOrDefault().EffectGroup.Effects.ToList().ForEach(entry => { parsedEffects.Add(entry.Effect); });
            }
            else
            {
                foreach (var token in dto.Effects.Split(",").ToList())
                {
                    if (!uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                    {
                        continue;
                    }
                    parsedEffects.Add(new EffectNumber(parsed));
                }
            }
            Effects = parsedEffects.ToArray();
            if (Effects.Length == 0)
                Effects = new EffectNumber[] { 0 };
            _durabilityPoints = dto.Durability;
            if (dto.Count < 0)
            {
                IsInvalid = true;
                return;
            }
            _stackCount = (uint)dto.Count;
            if (_stackCount == 0)
                _stackCount = 1;
            _enchantMana = dto.EnchantMP;
            _enchantLevel = dto.EnchantLvl;
            PurchaseDate = DateTimeOffset.FromUnixTimeSeconds(dto.PurchaseDate);
        }
        internal PlayerItem(Inventory inventory, ShopItemInfo itemInfo, ShopPrice price, byte color,
            EffectNumber[] effects,
            DateTimeOffset purchaseDate, uint count)
        {
            Inventory = inventory;
            Id = ItemIdGenerator.GetNextId();
            ItemNumber = itemInfo.ShopItem.ItemNumber;
            PriceType = itemInfo.PriceGroup.PriceType;
            PeriodType = price.PeriodType;
            Period = price.Period;
            DaysLeft = price.Period;
            Color = color;
            Effects = effects;
            PurchaseDate = purchaseDate;
            _durabilityPoints = price.Durability;
            _stackCount = count;
            NeedsToSave = true;
        }
        internal PlayerItem(Inventory inventory, ShopItemInfo itemInfo, ShopPrice price, byte color,
    EffectNumber[] effects,
    DateTimeOffset purchaseDate, int EnchantLvl, uint count)
        {
            Inventory = inventory;
            Id = ItemIdGenerator.GetNextId();
            ItemNumber = itemInfo.ShopItem.ItemNumber;
            PriceType = itemInfo.PriceGroup.PriceType;
            PeriodType = price.PeriodType;
            Period = price.Period;
            DaysLeft = price.Period;
            Color = color;
            Effects = effects;
            PurchaseDate = purchaseDate;
            _durabilityPoints = price.Durability;
            _enchantLevel = EnchantLvl;
            _stackCount = count;
            NeedsToSave = true;
        }
        internal bool ExistsInDatabase { get; set; }
        internal bool NeedsToSave { get; set; }
        public Inventory Inventory { get; }
        public ulong Id { get; }
        public ItemNumber ItemNumber { get; set; }
        public ItemPriceType PriceType { get; }
        public ItemPeriodType PeriodType { get; }
        public ushort Period { get; set; }
        public ushort DaysLeft { get; set; }
        public ushort HoursLeft { get; set; }
        public byte Color { get; }
        public EffectNumber[] Effects { get; set; }
        public DateTimeOffset PurchaseDate { get; }
        public int DurabilityLoss { get; set; }
        public uint EnchantMP
        {
            get => _enchantMana;
            set
            {
                if (_enchantMana == value)
                    return;
                _enchantMana = value;
                NeedsToSave = true;
            }
        }
        public int EnchantLvl
        {
            get => _enchantLevel;
            set
            {
                if (_enchantLevel == value)
                    return;
                _enchantLevel = value;
                NeedsToSave = true;
            }
        }
        public int Durability
        {
            get => _durabilityPoints;
            set
            {
                if (_durabilityPoints == value)
                    return;
                _durabilityPoints = value;
                NeedsToSave = true;
            }
        }
        public uint Count
        {
            get => _stackCount;
            set
            {
                if (_stackCount == value)
                    return;
                _stackCount = value;
                NeedsToSave = true;
            }
        }
        public DateTimeOffset CalculateExpireTime() => PeriodType == ItemPeriodType.Days ? PurchaseDate.AddDays(DaysLeft) : DateTimeOffset.MinValue;
        public long ExpireDate => PeriodType == ItemPeriodType.Days ? Expire() : -1;
        private long Expire()
        {
            switch (PeriodType)
            {
                case ItemPeriodType.None:
                    return uint.MaxValue;
                case ItemPeriodType.Days:
                    {
                        var remaining = PurchaseDate.AddDays(DaysLeft) - DateTime.Now;
                        if (remaining.Seconds > 0)
                            return (long)remaining.TotalSeconds;
                    }
                    return 0;
                case ItemPeriodType.Hours:
                    {
                        var remaining = PurchaseDate.AddHours(HoursLeft) - DateTime.Now;
                        if (remaining.Seconds > 0)
                            return (long)remaining.TotalSeconds;
                    }
                    return 0;
            }
            return 0;
        }
        public EffectNumber[] GetItemEffects()
        {
            if (Effects.Length == 0)
                return null;
            var effectTable = GameServer.Instance.ResourceCache.GetEffects();
            var resolved = new List<EffectNumber>();
            foreach (var current in Effects)
                resolved.Add(effectTable.GetValueOrDefault(current).Id);
            return resolved.ToArray();
        }
        public uint[] GetItemEffectsInt()
        {
            if (Effects.Length == 0)
                return null;
            var resolved = new List<uint>();
            foreach (var current in Effects)
            {
                if (!resolved.Contains(current.Id))
                {
                    resolved.Add(current.Id);
                }
            }
            return resolved.ToArray();
        }
        public uint[] GetItemEffectsInt2()
        {
            if (Effects.Length == 0)
                return null;
            var effectTable = GameServer.Instance.ResourceCache.GetEffects();
            var resolved = new List<uint>();
            foreach (var current in Effects)
                resolved.Add(effectTable.GetValueOrDefault(current).Id);
            return resolved.ToArray();
        }
        public ShopItem GetShopItem()
        {
            var shop = GameServer.Instance.ResourceCache.GetShop();
            return shop.GetItem(ItemNumber);
        }
        public ShopItemInfo GetShopItemInfo()
        {
            var shop = GameServer.Instance.ResourceCache.GetShop();
            return shop.GetItemInfo(ItemNumber, PriceType);
        }
        public ShopPrice GetShopPrice()
        {
            return GetShopItemInfo().PriceGroup.GetPrice(PeriodType, Period);
        }
        public ItemDurabilityInfoDto LoseDurability(int loss)
        {
            if (loss < 0)
                throw new ArgumentOutOfRangeException(nameof(loss));
            if (Inventory.Player.Room == null)
                throw new InvalidOperationException("Player is not inside a room");
            if (Durability == -1)
            {
                DurabilityLoss = 0;
            }
            else
            {
                Durability -= loss;
                DurabilityLoss = loss;
                if (Durability < 0)
                    Durability = 0;
            }
            return this.Map<PlayerItem, ItemDurabilityInfoDto>();
        }
        public uint CalculateRefund(ShopPrice price)
        {
            if (Count == 0)
                Count = 1;
            var shopprice = price.Price;
            if (shopprice * 0.15 < 0)
                return 0;
            return (uint)(shopprice * 0.15);
        }
        public uint CalculateRepair()
        {
            return 0;
        }
    }
}

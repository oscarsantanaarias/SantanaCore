using System;
using System.Collections.Generic;
using System.Linq;
using Santana.Database.Game;
using Santana.Resource;

namespace Santana.Shop
{
    internal class ShopItem
    {
        public ShopItem(ShopItemDto dto, ShopResources shopResources)
        {
            ItemNumber = dto.Id;
            Gender = (Gender)dto.RequiredGender;
            License = (ItemLicense)dto.RequiredLicense;
            ColorGroup = dto.Colors;
            UniqueColorGroup = dto.UniqueColors;
            MinLevel = dto.RequiredLevel;
            MaxLevel = dto.LevelLimit;
            MasterLevel = dto.RequiredMasterLevel;
            IsOneTimeUse = dto.IsOneTimeUse;
            IsDestroyable = dto.IsDestroyable;
            MainTab = dto.MainTab;
            SubTab = dto.SubTab;

            var infos = new List<ShopItemInfo>();
            try
            {
                foreach (var infoDto in dto.ItemInfos)
                    infos.Add(new ShopItemInfo(this, infoDto, shopResources));
            }
            catch (Exception failure)
            {
                Console.WriteLine(
                    $"[SHOP LOAD ERROR] Item={dto.Id} InfoCount={dto.ItemInfos?.Count() ?? -1} " +
                    $"Msg={failure.GetType().Name}: {failure.Message}");
                infos = new List<ShopItemInfo>();
            }

            ItemInfos = infos;
        }

        public ItemNumber ItemNumber { get; set; }
        public Gender Gender { get; set; }
        public ItemLicense License { get; set; }
        public int ColorGroup { get; set; }
        public int UniqueColorGroup { get; set; }
        public int MinLevel { get; set; }
        public int MaxLevel { get; set; }

        public int MasterLevel { get; set; }

        public bool IsOneTimeUse { get; set; }

        public bool IsDestroyable { get; set; }
        public byte MainTab { get; set; }
        public byte SubTab { get; set; }
        public IList<ShopItemInfo> ItemInfos { get; set; }

        public ShopItemInfo GetItemInfo(int id)
        {
            foreach (var info in ItemInfos)
                if (info.Id == id)
                    return info;

            return null;
        }

        public ShopItemInfo GetItemInfo(ItemNumber itemNumber)
        {
            foreach (var info in ItemInfos)
                if (info.ShopItem.ItemNumber == itemNumber)
                    return info;

            return null;
        }

        public ShopItemInfo GetItemInfo(ItemPriceType priceType)
        {
            foreach (var info in ItemInfos)
                if (info.PriceGroup.PriceType == priceType)
                    return info;

            return null;
        }
    }

    internal class ShopItemInfo
    {
        public ShopItemInfo(ShopItem shopItem, ShopItemInfoDto dto, ShopResources shopResources)
        {
            ShopItem = shopItem;
            Id = dto.Id;
            ShopInfoType = dto.Type;
            Discount = dto.DiscountPercentage;
            PriceGroup = shopResources.Prices[dto.PriceGroupId];
            EffectGroup = shopResources.Effects[dto.EffectGroupId];
        }

        public int Id { get; set; }
        public ShopPriceGroup PriceGroup { get; set; }
        public ShopEffectGroup EffectGroup { get; set; }
        public byte ShopInfoType { get; set; }
        public int Discount { get; set; }

        public ShopItem ShopItem { get; }
    }
}

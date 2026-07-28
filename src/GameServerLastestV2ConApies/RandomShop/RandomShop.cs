using Santana.Database.Game;
using Santana.Resource;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Santana.RandomShop
{
    internal class RandomShopItem
    {
        public RandomShopItem(FumbiShopItemDto dto)
        {
            CategoryId = (int)dto.GroupId;
            ShopItemId = (int)dto.ShopItemId;
            RewardValue = dto.RewardValue;
            Probability = dto.Probability;
            Grade = RandomShopResources.GradeToNum(dto.Grade);
            EffectGroup = dto.EffectGroup ?? "";
            ColorGroup = dto.ColorGroup ?? "";
            PeriodGroup = RandomShopResources.StdPeriodGroup;
            DefaultColor = dto.DefaultColor;

            var std = RandomShopResources.StdPeriods[0];
            ItemPeriodType = (ItemPeriodType)std.Type;
            Period = (uint)std.Period;
        }

        public int CategoryId { get; set; }
        public int ShopItemId { get; set; }
        public int RewardValue { get; set; }
        public int Probability { get; set; }
        public int Grade { get; set; }
        public string EffectGroup { get; set; }
        public string ColorGroup { get; set; }
        public string PeriodGroup { get; set; }
        public byte DefaultColor { get; set; }
        public int Color => DefaultColor;
        public ItemPeriodType ItemPeriodType { get; set; }
        public uint Period { get; set; }
    }

    internal class RandomShopCategoryInfo
    {
        public RandomShopCategoryInfo(FumbiShopItemGroupDto dto)
        {
            CategoryId = (int)dto.Id;
            NameKey = string.IsNullOrEmpty(dto.NameKey) ? $"N{CategoryId}" : dto.NameKey;
            DescKey = string.IsNullOrEmpty(dto.DescKey) ? $"D{CategoryId}" : dto.DescKey;
            PiceType = (dto.PriceType ?? "pen").ToLowerInvariant();
            Price = dto.Price;
            RequiredGender = dto.RequiredGender;
            EnabledType = dto.EnabledType;
        }

        public int CategoryId { get; set; }
        public string NameKey { get; set; }
        public string DescKey { get; set; }
        public string PiceType { get; set; }
        public int Price { get; set; }
        public byte RequiredGender { get; set; }
        public byte EnabledType { get; set; }
    }
}

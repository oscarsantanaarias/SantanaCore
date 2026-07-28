using System.Collections.Generic;
using Santana.Database.Game;

namespace Santana.Shop
{
    internal class ShopPriceGroup
    {
        public ShopPriceGroup(ShopPriceGroupDto dto)
        {
            Id = dto.Id;
            PriceType = (ItemPriceType)dto.PriceType;
            Name = dto.Name;

            var built = new List<ShopPrice>();
            foreach (var priceDto in dto.ShopPrices)
                built.Add(new ShopPrice(priceDto));
            Prices = built;
        }

        public int Id { get; set; }
        public ItemPriceType PriceType { get; set; }
        public string Name { get; set; }
        public IList<ShopPrice> Prices { get; set; }

        public ShopPrice GetPrice(int id)
        {
            foreach (var price in Prices)
                if (price.Id == id)
                    return price;

            return null;
        }

        public ShopPrice GetPrice(ItemPeriodType periodType, ushort period)
        {
            foreach (var price in Prices)
                if (price.PeriodType == periodType && price.Period == period)
                    return price;

            return null;
        }
    }

    internal class ShopPrice
    {
        public ShopPrice(ShopPriceDto dto)
        {
            Id = dto.Id;
            PeriodType = (ItemPeriodType)dto.PeriodType;
            Period = (ushort)dto.Period;
            Price = dto.Price;
            CanRefund = dto.IsRefundable;
            Durability = dto.Durability;
            IsEnabled = dto.IsEnabled;
        }

        public int Id { get; set; }
        public ItemPeriodType PeriodType { get; set; }
        public ushort Period { get; set; }
        public int Price { get; set; }
        public bool CanRefund { get; set; }
        public int Durability { get; set; }
        public bool IsEnabled { get; set; }
    }
}

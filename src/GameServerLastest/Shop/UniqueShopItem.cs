using Santana.Database.Game;
using Santana.Resource;

namespace Santana.Shop
{
  internal class UniqueShopItem
  {
    public UniqueShopItem(ShopItemDto dto, ShopResources shopResources)
    {
    }

    public ItemNumber ItemNumber { get; set; }
    public int ShopId { get; set; }
    public int Discount { get; set; }
    public ItemPeriodType PeriodType { get; set; }
    public int Period { get; set; }
    public int Color { get; set; }
    public bool Enabled { get; set; }

  }

  internal class UniqueShopItemInfo
  {
    public UniqueShopItemInfo(ShopItem shopItem, ShopItemInfoDto dto, ShopResources shopResources)
    {
    }

    public ItemNumber ItemNumber { get; set; }
    public int ShopId { get; set; }
    public int Discount { get; set; }
    public ItemPeriodType PeriodType { get; set; }
    public int Period { get; set; }
    public int Color { get; set; }
    public bool Enabled { get; set; }
  }
}

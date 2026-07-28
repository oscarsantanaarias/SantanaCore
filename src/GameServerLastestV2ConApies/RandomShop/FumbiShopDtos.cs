using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Santana.Database.Game
{
    [Table("fumbi_shop_item_groups")]
    public class FumbiShopItemGroupDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string NameKey { get; set; }
        public string DescKey { get; set; }
        public string PriceType { get; set; }
        public int Price { get; set; }
        public byte RequiredGender { get; set; }
        public byte EnabledType { get; set; }
    }

    [Table("fumbi_shop_effect_groups")]
    public class FumbiShopEffectGroupDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string Name { get; set; }
        public int EffectGroupId { get; set; }
        public int CProbability { get; set; }
        public int SProbability { get; set; }
        public string Grade { get; set; }
    }

    [Table("fumbi_shop_color_groups")]
    public class FumbiShopColorGroupDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public string Name { get; set; }
        public byte Color { get; set; }
        public int Probability { get; set; }
        public string Grade { get; set; }
    }

    [Table("fumbi_shop_items")]
    public class FumbiShopItemDto
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }
        public long GroupId { get; set; }
        public byte CategoryId { get; set; }
        public byte RewardValue { get; set; }
        public long ShopItemId { get; set; }
        public string PeriodGroup { get; set; }
        public string EffectGroup { get; set; }
        public string ColorGroup { get; set; }
        public byte DefaultColor { get; set; }
        public int Probability { get; set; }
        public string Grade { get; set; }
        public bool IsEnabled { get; set; }
    }
}

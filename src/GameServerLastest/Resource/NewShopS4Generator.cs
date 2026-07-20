using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Santana;
using Santana.Resource;
using Santana.Shop;

namespace Santana.ShopS4
{
    internal static class NewShopS4Generator
    {
        private const string StampFile = "_eu_new_shop.version";

        private static readonly string[] TargetFiles =
        {
            "_eu_new_shop_price.s4",
            "_eu_new_shop_effect.s4",
            "_eu_new_shop_item.s4",
            "_eu_new_shop_unique_item.s4"
        };

        internal sealed class NewShopS4Bundle
        {
            public byte[] Prices { get; set; }
            public byte[] Effects { get; set; }
            public byte[] Items { get; set; }
            public byte[] UniqueItems { get; set; }
        }

        public static void GenerateIfVersionChanged(
            string version,
            ICollection<ShopPriceGroup> prices,
            ICollection<ShopEffectGroup> effects,
            ICollection<ShopItem> items)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;

            var payload = Pack(version, prices, effects, items);
            var wroteAnything = false;

            foreach (var folder in EnumerateTargetFolders())
            {
                var stampPath = Path.Combine(folder, StampFile);
                var stampFresh = false;

                if (File.Exists(stampPath))
                {
                    try
                    {
                        stampFresh = string.Equals(File.ReadAllText(stampPath).Trim(), version.Trim(), StringComparison.Ordinal);
                    }
                    catch
                    {
                        stampFresh = false;
                    }
                }

                if (stampFresh)
                    continue;

                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, TargetFiles[0]), payload.Prices);
                File.WriteAllBytes(Path.Combine(folder, TargetFiles[1]), payload.Effects);
                File.WriteAllBytes(Path.Combine(folder, TargetFiles[2]), payload.Items);
                File.WriteAllBytes(Path.Combine(folder, TargetFiles[3]), payload.UniqueItems);
                File.WriteAllText(Path.Combine(folder, StampFile), version);
                wroteAnything = true;
            }

            if (wroteAnything)
            {
                Console.WriteLine(
                    $"[NewShop] generated s4 files version={version} price={payload.Prices.Length} effect={payload.Effects.Length} item={payload.Items.Length} unique={payload.UniqueItems.Length}");
            }
        }

        private static NewShopS4Bundle Pack(
            string version,
            ICollection<ShopPriceGroup> prices,
            ICollection<ShopEffectGroup> effects,
            ICollection<ShopItem> items)
        {
            var priceDoc = RenderPrices(version, prices);
            var effectDoc = RenderEffects(version, effects);
            var itemDoc = RenderItems(version, items);
            var uniqueDoc = RenderUniqueItems(version, items);

            return new NewShopS4Bundle
            {
                Prices = S4Zip.EncryptS4(Encoding.UTF8.GetBytes(priceDoc)),
                Effects = S4Zip.EncryptS4(Encoding.UTF8.GetBytes(effectDoc)),
                Items = S4Zip.EncryptS4(Encoding.UTF8.GetBytes(itemDoc)),
                UniqueItems = S4Zip.EncryptS4(Encoding.UTF8.GetBytes(uniqueDoc))
            };
        }

        private static IEnumerable<string> EnumerateTargetFolders()
        {
            var folders = new List<string> { Path.Combine(AppContext.BaseDirectory, "shop") };

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var clientShop = Path.Combine(desktop, "S4Client", "shop");
            if (Directory.Exists(Path.GetDirectoryName(clientShop)))
                folders.Add(clientShop);

            return folders.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string RenderPrices(string version, ICollection<ShopPriceGroup> prices)
        {
            var sb = new StringBuilder();
            foreach (var group in prices.OrderBy(p => p.Id))
            {
                sb.Append(
                    $"  <price_group group_name=\"{Escape($"{group.Name}_{group.Id}")}\" shop_id=\"{(int)group.PriceType}\">\n");

                foreach (var row in group.Prices.OrderBy(p => p.PeriodType).ThenBy(p => p.Period))
                {
                    sb.Append(
                        $"    <price period_type=\"{PeriodLabel(row.PeriodType)}\" period=\"{row.Period}\" price=\"{row.Price}\" refund=\"{FlagText(row.CanRefund)}\" gauge=\"{row.Durability}\" shop_open=\"{FlagText(row.IsEnabled)}\" />\n");
                }

                sb.Append("  </price_group>\n");
            }

            return SealDocument("price_group_list", version, sb.ToString());
        }

        private static string RenderEffects(string version, ICollection<ShopEffectGroup> effects)
        {
            var sb = new StringBuilder();
            foreach (var group in effects.OrderBy(e => e.MainEffect))
            {
                sb.Append($"  <effect_match key=\"{group.MainEffect}\">\n");
                foreach (var entry in group.Effects.OrderBy(e => e.Effect))
                    sb.Append($"    <effects effect_id=\"{entry.Effect}\" />\n");

                sb.Append("  </effect_match>\n");
            }

            return SealDocument("effect_match_list", version, sb.ToString());
        }

        private static string RenderItems(string version, ICollection<ShopItem> items)
        {
            var sb = new StringBuilder();
            foreach (var entry in items.OrderBy(i => i.ItemNumber.Id))
            {
                sb.Append(
                    $"  <item key=\"{entry.ItemNumber.Id}\" name=\"\" gender=\"{(byte)entry.Gender}\" license=\"{(byte)entry.License}\" color_group=\"{entry.ColorGroup}\" unique_color_group=\"{entry.UniqueColorGroup}\" level_limit_min=\"{entry.MinLevel}\" level_limit_max=\"{entry.MaxLevel}\" master_level_limit=\"{entry.MasterLevel}\" repair_money=\"0\" use_at_once=\"{FlagText(entry.IsOneTimeUse)}\" not_discard=\"{FlagText(!entry.IsDestroyable)}\" shop_main_category=\"{entry.MainTab}\" shop_sub_category=\"{entry.SubTab}\" shop_order=\"10\">\n");

                foreach (var info in entry.ItemInfos.OrderBy(i => i.ShopInfoType))
                {
                    var state = HasEnabledPrice(info) ? "on" : "off";
                    var priceGroupName = info.PriceGroup == null
                        ? string.Empty
                        : Escape($"{info.PriceGroup.Name}_{info.PriceGroup.Id}");
                    sb.Append(
                        $"    <shopinfo onoff=\"{state}\" shop_id=\"{info.ShopInfoType}\" discount=\"{info.Discount}\" effect_match=\"{info.EffectGroup?.MainEffect ?? 0}\" price_group=\"{priceGroupName}\" />\n");
                }

                sb.Append("  </item>\n");
            }

            return SealDocument("item_list", version, sb.ToString());
        }

        private static string RenderUniqueItems(string version, ICollection<ShopItem> items)
        {
            var sb = new StringBuilder();
            foreach (var entry in items
                         .Where(i => i.UniqueColorGroup > 0)
                         .OrderBy(i => i.ItemNumber.Id))
            {
                var live = entry.ItemInfos.FirstOrDefault(HasEnabledPrice);
                var state = live != null ? "on" : "off";
                var shopId = live?.ShopInfoType ?? 0;
                var periodKind = 0;
                var periodLength = 0;
                var refundFlag = "N";

                if (live != null)
                {
                    var activePrice = live.PriceGroup?.Prices.FirstOrDefault(p => p.IsEnabled);
                    if (activePrice != null)
                    {
                        periodKind = activePrice.PeriodType == ItemPeriodType.None
                            ? 0
                            : (int)activePrice.PeriodType;
                        periodLength = activePrice.Period;
                        refundFlag = activePrice.CanRefund ? "Y" : "N";
                    }
                }

                sb.Append(
                    $"  <uniqueitem key=\"{entry.ItemNumber.Id}\" shopid=\"{shopId}\" discount=\"{live?.Discount ?? 0}\" periodtype=\"{periodKind}\" period=\"{periodLength}\" color=\"0\" onoff=\"{state}\" reward=\"0\" start_date=\"\" end_date=\"\" refund=\"{refundFlag}\" />\n");
            }

            return SealDocument("unique_item_list", version, sb.ToString());
        }

        private static bool HasEnabledPrice(ShopItemInfo info) =>
            info?.PriceGroup?.Prices?.Any(p => p.IsEnabled) == true;

        private static string SealDocument(string rootName, string version, string body)
        {
            var draft = $"<{rootName} version=\"{Escape(version)}\" size=\"0\">\n{body}</{rootName}>";
            var byteLength = Encoding.UTF8.GetByteCount(draft);
            return draft.Replace("size=\"0\"", $"size=\"{byteLength}\"");
        }

        private static string PeriodLabel(ItemPeriodType periodType)
        {
            switch (periodType)
            {
                case ItemPeriodType.Hours:
                    return "HOURS";
                case ItemPeriodType.Days:
                    return "DAYS";
                case ItemPeriodType.Units:
                    return "USECNT";
                default:
                    return "NONE";
            }
        }

        private static string FlagText(bool value) => value ? "true" : "false";

        private static string Escape(string value) =>
            (value ?? string.Empty)
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
    }
}

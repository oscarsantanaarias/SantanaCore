using SantanaLib.IO;
using ProudNetSrc;
using Santana.Database.Game;
using Santana.RandomShop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Santana.Resource
{
    internal class RandomShopResources
    {
        public const string StdPeriodGroup = "PStd";
        public static readonly (int Period, int Type)[] StdPeriods =
        {
            (1, 3),
            (3, 3),
            (7, 3),
            (30, 3),
            (0, 1),
        };

        private Dictionary<int, RandomShopItem> _catalogById;
        private Dictionary<int, List<RandomShopItem>> _itemsByCategory;
        private Dictionary<int, RandomShopCategoryInfo> _categories;
        private Dictionary<string, List<(uint Effect, int Grade)>> _effectPools;

        public IReadOnlyDictionary<int, RandomShopCategoryInfo> Category => _categories;

        public byte[] RandomShopItems = Array.Empty<byte>();

        public string Version { get; private set; }

        public void Load()
        {
            using (var db = GameDatabase.Open())
            {
                var packages = DbUtil.Find<FumbiShopItemGroupDto>(db).ToList();
                var fumbiEffects = DbUtil.Find<FumbiShopEffectGroupDto>(db).ToList();
                var colors = DbUtil.Find<FumbiShopColorGroupDto>(db).ToList();
                var lineups = DbUtil.Find<FumbiShopItemDto>(db).Where(x => x.IsEnabled).ToList();
                var effectGroupEffect = DbUtil.Find<ShopEffectGroupDto>(db)
                    .GroupBy(x => x.Id)
                    .ToDictionary(g => g.Key, g => g.First().Effect);

                _categories = new Dictionary<int, RandomShopCategoryInfo>();
                foreach (var p in packages)
                {
                    var cat = new RandomShopCategoryInfo(p);
                    _categories[cat.CategoryId] = cat;
                }

                _effectPools = new Dictionary<string, List<(uint, int)>>();
                foreach (var e in fumbiEffects)
                {
                    var name = e.Name ?? "";
                    if (!_effectPools.TryGetValue(name, out var list))
                    {
                        list = new List<(uint, int)>();
                        _effectPools[name] = list;
                    }
                    var effect = effectGroupEffect.TryGetValue(e.EffectGroupId, out var ev) ? ev : 0u;
                    list.Add((effect, GradeToNum(e.Grade)));
                }

                _catalogById = new Dictionary<int, RandomShopItem>();
                _itemsByCategory = new Dictionary<int, List<RandomShopItem>>();
                foreach (var l in lineups)
                {
                    var item = new RandomShopItem(l);
                    _catalogById[item.ShopItemId] = item;
                    if (!_itemsByCategory.TryGetValue(item.CategoryId, out var bucket))
                    {
                        bucket = new List<RandomShopItem>();
                        _itemsByCategory[item.CategoryId] = bucket;
                    }
                    bucket.Add(item);
                }

                using (var w = new BinaryWriter(new MemoryStream()))
                {
                    w.Write(packages.Count);
                    foreach (var p in packages)
                    {
                        var id = (int)p.Id;
                        w.Write(id);
                        w.Write((byte)1);
                        w.Write((short)1);
                        w.WriteProudString(string.IsNullOrEmpty(p.NameKey) ? $"N{id}" : p.NameKey);
                        w.WriteProudString(string.IsNullOrEmpty(p.DescKey) ? $"D{id}" : p.DescKey);
                        w.WriteProudString(OpenToStr(p.EnabledType));
                        w.WriteProudString(PriceTypeWire(p.PriceType));
                        w.Write(p.Price);
                        w.WriteProudString(GenderToStr(p.RequiredGender));
                    }

                    w.Write(fumbiEffects.Count);
                    foreach (var e in fumbiEffects)
                    {
                        var effect = effectGroupEffect.TryGetValue(e.EffectGroupId, out var ev) ? ev : 0u;
                        w.Write(e.CProbability);
                        w.Write((byte)(GradeToNum(e.Grade) & 0xff));
                        w.WriteProudString(e.Name ?? "");
                        w.Write((int)effect);
                    }

                    w.Write(colors.Count);
                    foreach (var c in colors)
                    {
                        w.Write(c.Probability);
                        w.Write((byte)(GradeToNum(c.Grade) & 0xff));
                        w.WriteProudString(c.Name ?? "");
                        w.Write((byte)(c.Color & 0xff));
                    }

                    w.Write(lineups.Count);
                    foreach (var l in lineups)
                    {
                        w.Write(l.Probability);
                        w.Write((byte)(GradeToNum(l.Grade) & 0xff));
                        w.Write((int)l.GroupId);
                        w.WriteProudString(RewardTypeStr(l.ShopItemId));
                        w.Write((int)l.RewardValue);
                        w.Write((int)l.ShopItemId);
                        w.WriteProudString(l.ColorGroup ?? "");
                        w.WriteProudString(l.EffectGroup ?? "");
                        w.WriteProudString(StdPeriodGroup);
                        w.Write((int)l.DefaultColor);
                    }

                    w.Write(StdPeriods.Length);
                    foreach (var pe in StdPeriods)
                    {
                        w.Write((int)100);
                        w.Write((byte)0);
                        w.WriteProudString(StdPeriodGroup);
                        w.Write(pe.Type);
                        w.Write(pe.Period);
                    }

                    RandomShopItems = w.ToArray();
                }

                var versionRow = DbUtil.Find<RandomShopVersionDto>(db).FirstOrDefault();
                Version = versionRow?.Version ?? "1";

                Console.WriteLine($"[RandomShop] built the fumbi blob from packages={packages.Count} effects={fumbiEffects.Count} " +
                                  $"colors={colors.Count} lineups={lineups.Count} bytes={RandomShopItems.Length} version={Version}");
            }
        }

        public void Clear()
        {
            _catalogById?.Clear();
            _itemsByCategory?.Clear();
            _categories?.Clear();
            _effectPools?.Clear();

            RandomShopItems = Array.Empty<byte>();
            Version = "";
        }

        public RandomShopItem GetItem(int category)
        {
            return _itemsByCategory != null && _itemsByCategory.TryGetValue(category, out var bucket) && bucket.Count > 0
                ? bucket[0]
                : null;
        }

        public RandomShopItem[] GetItems(int category)
        {
            return _itemsByCategory != null && _itemsByCategory.TryGetValue(category, out var bucket)
                ? bucket.ToArray()
                : Array.Empty<RandomShopItem>();
        }

        public RandomShopCategoryInfo GetCategory(int category)
        {
            return _categories != null && _categories.TryGetValue(category, out var cat) ? cat : null;
        }

        public List<(uint Effect, int Grade)> GetEffectPool(string name)
        {
            return _effectPools != null && name != null && _effectPools.TryGetValue(name, out var pool)
                ? pool
                : new List<(uint, int)>();
        }

        public (int Period, int Type)[] GetPeriodPool(string name)
        {
            return StdPeriods;
        }

        public static int GradeToNum(string g)
        {
            var s = (g ?? "").Trim();
            if (s.Length > 0 && int.TryParse(s, out var n))
                return n;
            switch (s.ToLowerInvariant())
            {
                case "common": return 0;
                case "uncommon": return 5;
                case "rare": return 10;
                case "epic": return 15;
                case "unique": return 20;
                case "legendary": return 30;
                default: return 0;
            }
        }

        private static string GenderToStr(byte g) => g == 1 ? "man" : g == 2 ? "woman" : "both";
        private static string OpenToStr(byte e) => e == 0 ? "off" : e == 2 ? "new" : "on";
        private static string PriceTypeWire(string pt)
        {
            var s = (pt ?? "pen").ToLowerInvariant();
            return s == "ap" ? "cash" : s;
        }

        private static string RewardTypeStr(long itemNumber)
        {
            if (itemNumber >= 2000000 && itemNumber < 3000000) return "weapon";
            if (itemNumber >= 1000000 && itemNumber < 2000000) return "costum";
            return "Others";
        }
    }
}

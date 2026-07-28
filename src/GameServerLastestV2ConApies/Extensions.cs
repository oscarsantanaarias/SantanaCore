using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SantanaLib.IO;
using Santana.Database.Auth;
using Santana.Network;
using Santana.Shop;
using ProudNetSrc;
using Serilog;
using Santana.RandomShop;
namespace Santana
{
    internal static class EnumerableExtentions
    {
        public static void ShuffleArr<T>(T[] array)
        {
            if (array is null)
                throw new ArgumentNullException(nameof(array));
            var dice = new Random();
            for (var pos = 0; pos < array.Length - 1; ++pos)
            {
                var pick = dice.Next(pos, array.Length);
                (array[pick], array[pos]) = (array[pos], array[pick]);
            }
        }
        public static IEnumerable<T> ShuffleSecure<T>(this IEnumerable<T> source)
        {
            var secureDice = new SecureRandom();
            var pool = source.ToArray();
            for (var cursor = pool.Length - 1; cursor >= 0; cursor--)
            {
                var chosen = secureDice.Next(cursor + 1);
                yield return pool[chosen];
                pool[chosen] = pool[cursor];
            }
        }
        public static T AnyOrDefault<T>(this IList<T> e, Func<T, double> weightSelector)
        {
            if (e.Count < 1)
                return default(T);
            if (e.Count == 1)
                return e[0];
            var weightTable = new double[e.Count];
            var total = 0d;
            for (var idx = 0; idx < e.Count; idx++)
            {
                var wgt = Math.Max(weightSelector(e[idx]), 0);
                weightTable[idx] = wgt;
                total += wgt;
            }
            var roll = new SecureRandom().NextDouble();
            for (var idx = 0; idx < weightTable.Length; idx++)
            {
                var chance = total == 0
                    ? 1 / (double)e.Count
                    : weightTable[idx] / total;
                if (roll < chance)
                    return e[idx];
                roll -= chance;
            }
            throw new Exception("Should not happen");
        }
    }
    internal static class Extensions
    {
        public static bool CheckValidFormatHtmlColor(string inputColor)
        {
            if (System.Text.RegularExpressions.Regex.Match(inputColor, "^(?:[0-9a-fA-F]{3}){1,2}$").Success)
                return true;
            return System.Drawing.Color.FromName(inputColor).IsKnownColor;
        }
        public static ILogger ForAccount(this ILogger logger, ulong id, string user,
        SecurityLevel securityLevel = SecurityLevel.User)
        {
            if (user == string.Empty)
                user = "n/A";
            if (user.Length > 8)
                user = user.Substring(0, 8) + "..";
            var idTag = $"({id})";
            var label = $"{user}{idTag}";
            const int cap = 11;
            if (idTag.Length < cap)
            {
                if (label.Length > cap)
                {
                    var keep = label.Length - idTag.Length - (label.Length - cap);
                    user = keep < 0 ? "" : user.Substring(0, keep);
                    label = $"{user}{idTag}";
                }
            }
            else
            {
                idTag = idTag.Substring(0, idTag.Length - (idTag.Length - cap));
                label = idTag;
            }
            return logger
                .ForContext("Accid", id)
                .ForContext("Accuser", label)
                .ForContext("Acclevel", securityLevel);
        }
        public static ILogger ForAccount(this ILogger logger, AccountDto account)
        {
            var display = string.IsNullOrEmpty(account.Nickname) ? account.Username : account.Nickname;
            return logger.ForAccount((ulong)account.Id, display, (SecurityLevel)account.SecurityLevel);
        }
        public static ILogger ForAccount(this ILogger logger, Account account)
        {
            var display = string.IsNullOrEmpty(account.Nickname) ? account.Username : account.Nickname;
            return logger.ForAccount(account.Id, display, account.SecurityLevel);
        }
        public static ILogger ForAccount(this ILogger logger, Player player)
        {
            return logger.ForAccount(player.Account);
        }
        public static ILogger ForAccount(this ILogger logger, GameSession session)
        {
            return session.IsLoggedIn() ? logger.ForAccount(session.Player) : logger;
        }
        public static ILogger ForAccount(this ILogger logger, ChatSession session)
        {
            return session.IsLoggedIn() ? logger.ForAccount(session.GameSession.Player) : logger;
        }
        public static bool IsLoggedIn(this GameSession session)
        {
            return !string.IsNullOrWhiteSpace(session?.Player?.Account?.Nickname) && session.IsConnected;
        }
        public static bool IsLoggedIn(this ChatSession session)
        {
            return !string.IsNullOrWhiteSpace(session?.GameSession?.Player?.Account?.Nickname) && session.IsConnected;
        }
        public static bool IsLoggedIn(this Player plr)
        {
            return (plr?.Session?.IsLoggedIn() ?? false) && (plr?.ChatSession?.IsLoggedIn() ?? false);
        }
        public static void Serialize(this BinaryWriter w, ICollection<ShopPriceGroup> value)
        {
            w.Write(value.Count);
            foreach (var priceGroup in value)
            {
                w.WriteProudString($"{priceGroup.Name}_{priceGroup.Id}");
                w.WriteEnum(priceGroup.PriceType);
                w.Write(priceGroup.Prices.Count);
                foreach (var row in priceGroup.Prices)
                {
                    w.WriteEnum(row.PeriodType);
                    w.Write(row.Period);
                    w.Write(row.Price);
                    w.Write(row.CanRefund);
                    w.Write(row.Durability);
                    w.Write(row.IsEnabled);
                }
            }
        }
        public static void Serialize(this BinaryWriter w, ICollection<ShopEffectGroup> value)
        {
            w.Write(value.Count);
            foreach (var effectGroup in value)
            {
                w.Write(effectGroup.MainEffect);
                w.Write(effectGroup.Effects.Count);
                foreach (var fx in effectGroup.Effects)
                    w.Write(fx.Effect);
            }
        }
        public static void Serialize(this BinaryWriter w, ICollection<ShopItem> value)
        {
            w.Write(value.Count);
            var slot = 0;
            foreach (var shopItem in value)
            {
                if (shopItem == null)
                {
                    throw new NullReferenceException($"ShopItem null at index {slot}");
                }
                if (shopItem.ItemInfos == null)
                {
                    throw new NullReferenceException($"ShopItem {shopItem.ItemNumber} ItemInfos null");
                }
                w.Write(shopItem.ItemNumber);
                switch (shopItem.Gender)
                {
                    case Gender.Female:
                        w.Write((uint)1);
                        break;
                    case Gender.Male:
                        w.Write((uint)0);
                        break;
                    case Gender.None:
                        w.Write((uint)2);
                        break;
                }
                w.Write((ushort)shopItem.License);
                w.Write((ushort)shopItem.ColorGroup);
                w.Write((ushort)shopItem.UniqueColorGroup);
                w.Write((ushort)shopItem.MinLevel);
                w.Write((ushort)shopItem.MaxLevel);
                w.Write((ushort)shopItem.MasterLevel);
                w.Write(0);
                w.Write(shopItem.IsOneTimeUse);
                w.Write(!shopItem.IsDestroyable);
                w.Write((ushort)shopItem.MainTab);
                w.Write((ushort)shopItem.SubTab);
                w.Write((ushort)1);
                w.Write(shopItem.ItemInfos.Count);
                var infoSlot = 0;
                foreach (var itemInfo in shopItem.ItemInfos)
                {
                    if (itemInfo == null)
                    {
                        throw new NullReferenceException($"ShopItem {shopItem.ItemNumber} ItemInfo null at index {infoSlot}");
                    }
                    if (itemInfo.PriceGroup == null)
                    {
                        throw new NullReferenceException($"ShopItem {shopItem.ItemNumber} Info {itemInfo.Id} PriceGroup null");
                    }
                    if (itemInfo.EffectGroup == null)
                    {
                        throw new NullReferenceException($"ShopItem {shopItem.ItemNumber} Info {itemInfo.Id} EffectGroup null");
                    }
                    w.WriteProudString(((ShopInfoTypeEnum)itemInfo.ShopInfoType).ToString().Replace("@", ""));
                    w.WriteEnum(itemInfo.PriceGroup.PriceType);
                    w.Write((ushort)itemInfo.Discount);
                    w.WriteProudString($"{itemInfo.PriceGroup.Name}_{itemInfo.PriceGroup.Id}");
                    w.Write(itemInfo.EffectGroup.MainEffect);
                    infoSlot++;
                }
                slot++;
            }
        }
        public static void SerializeUniqueItems(this BinaryWriter w, ICollection<ShopItem> items)
        {
            var uniqueRows = new List<(ShopItem Item, ShopItemInfo Info, byte Color)>();
            foreach (var shopItem in items)
            {
                if (shopItem == null)
                    continue;
                foreach (var itemInfo in shopItem.ItemInfos)
                {
                    if (itemInfo?.PriceGroup?.Prices?.Any(p => p.IsEnabled) != true)
                        continue;
                    var qualifies = shopItem.UniqueColorGroup > 0
                        || (shopItem.ColorGroup > 1 && itemInfo.ShopInfoType == 1);
                    if (!qualifies)
                        continue;
                    uniqueRows.Add((shopItem, itemInfo, 0));
                }
            }
            w.Write(uniqueRows.Count);
            foreach (var (shopItem, itemInfo, colorId) in uniqueRows
                         .OrderBy(r => r.Item.ItemNumber.Id)
                         .ThenBy(r => r.Info.ShopInfoType)
                         .ThenBy(r => r.Color))
            {
                var activePrice = itemInfo.PriceGroup.Prices.First(p => p.IsEnabled);
                var periodKind = activePrice.PeriodType == ItemPeriodType.None
                    ? 0u
                    : (uint)activePrice.PeriodType;
                w.Write((uint)shopItem.ItemNumber.Id);
                w.Write((uint)itemInfo.ShopInfoType);
                w.Write((ushort)itemInfo.Discount);
                w.Write(periodKind);
                w.Write((ushort)activePrice.Period);
                w.Write(colorId);
                w.WriteProudString("on");
                w.Write(activePrice.CanRefund);
                w.Write(0);
                w.WriteProudString("");
                w.WriteProudString("");
            }
        }
    }
}

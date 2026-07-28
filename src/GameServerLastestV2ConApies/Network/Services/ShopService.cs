using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using SantanaLib;
using SantanaLib.DotNetty.Handlers.MessageHandling;
using SantanaLib.IO;
using Dapper;
using ExpressMapper.Extensions;
using Santana.Network.Data.Game;
using Santana.Network.Message.Chat;
using Santana.Network.Message.Game;
using ProudNetSrc;
using ProudNetSrc.Handlers;
using Serilog;
using Serilog.Core;
using Santana.Database.Game;
using Santana.Resource;
using Santana.RandomShop;
using Org.BouncyCastle.Bcpg;
using System.Text.Json;
using Org.BouncyCastle.Asn1.Utilities;
using static System.Collections.Specialized.BitVector32;
namespace Santana.Network.Services
{
    internal class ShopService : ProudMessageHandler
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ShopService));
        private static string CollectBookVersion = "20171116121051";
        private static readonly bool ForceCollectBookRebuild = false;
        private const int CollectBookForcedAckLimit = 3;
        public static bool IsCollectBookForceRebuild => ForceCollectBookRebuild;
        private static readonly Dictionary<ulong, DateTime> collectPushClock = new Dictionary<ulong, DateTime>();
        private static readonly Dictionary<ulong, DateTime> collectRebuildClock = new Dictionary<ulong, DateTime>();
        private static readonly Dictionary<ulong, CollectBookForcedUpdate> collectPinnedMap = new Dictionary<ulong, CollectBookForcedUpdate>();
        private static readonly object collectCacheLock = new object();
        private static CollectBookCacheSnapshot collectCacheRef;
        private static readonly TimeSpan CollectBookDuplicateUpdateWindow = TimeSpan.FromSeconds(2);
        private static readonly object newShopCacheLock = new object();
        private static NewShopCacheSnapshot newShopCacheRef;
        private static readonly Dictionary<ulong, DateTime> newShopPushClock = new Dictionary<ulong, DateTime>();
        private static readonly TimeSpan NewShopDuplicateUpdateWindow = TimeSpan.FromSeconds(2);
        public static void ForceCollectBookVersion(
            ulong accountId,
            string version,
            bool sendUpdateInfo,
            byte[] payloadOverride = null,
            string payloadLabel = null,
            int? unk2Override = null,
            int? unk3Override = null,
            string unk4Override = null)
        {
            if (string.IsNullOrWhiteSpace(version))
                return;
            lock (collectPinnedMap)
                collectPinnedMap[accountId] = new CollectBookForcedUpdate(
                    version.Trim(),
                    sendUpdateInfo,
                    payloadOverride,
                    payloadLabel,
                    unk2Override,
                    unk3Override,
                    unk4Override);
        }
        private static void MarkCollectBookForcedSent(ulong accountId, CollectBookForcedUpdate forcedUpdate)
        {
            if (forcedUpdate == null)
                return;
            lock (collectPinnedMap)
            {
                forcedUpdate.Sends++;
                if (forcedUpdate.SendUpdateInfo || forcedUpdate.Sends >= CollectBookForcedAckLimit)
                    collectPinnedMap.Remove(accountId);
            }
        }
        private static void ClearCollectBookForcedVersion(ulong accountId)
        {
            lock (collectPinnedMap)
                collectPinnedMap.Remove(accountId);
        }
        private static bool WasCollectBookUpdateSentRecently(ulong accountId)
        {
            lock (collectPushClock)
            {
                if (!collectPushClock.TryGetValue(accountId, out var lastSentUtc))
                    return false;
                return (DateTime.UtcNow - lastSentUtc) <= CollectBookDuplicateUpdateWindow;
            }
        }
        private static void MarkCollectBookUpdateSent(ulong accountId)
        {
            lock (collectPushClock)
                collectPushClock[accountId] = DateTime.UtcNow;
        }
        private static int CompareCollectBookVersions(string left, string right)
        {
            left ??= string.Empty;
            right ??= string.Empty;
            if (long.TryParse(left, out var leftNumber) && long.TryParse(right, out var rightNumber))
                return leftNumber.CompareTo(rightNumber);
            return string.CompareOrdinal(left, right);
        }
        private static string BuildCollectBookForcedSessionVersion(string clientVersion, string serverVersion)
        {
            clientVersion = string.IsNullOrWhiteSpace(clientVersion) ? "0" : clientVersion.Trim();
            serverVersion = string.IsNullOrWhiteSpace(serverVersion) ? "0" : serverVersion.Trim();
            if (long.TryParse(clientVersion, out var clientNumber))
            {
                var nextNumber = clientNumber + 1;
                var width = Math.Max(clientVersion.Length, serverVersion.Length);
                return nextNumber.ToString("D" + Math.Max(width, 14));
            }
            var fallback = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            return CompareCollectBookVersions(fallback, clientVersion) > 0 ? fallback : clientVersion + "1";
        }
        private sealed class CollectBookForcedUpdate
        {
            public CollectBookForcedUpdate(
                string version,
                bool sendUpdateInfo,
                byte[] payloadOverride = null,
                string payloadLabel = null,
                int? unk2Override = null,
                int? unk3Override = null,
                string unk4Override = null)
            {
                Version = version;
                SendUpdateInfo = sendUpdateInfo;
                PayloadOverride = payloadOverride;
                PayloadLabel = payloadLabel ?? string.Empty;
                Unk2Override = unk2Override;
                Unk3Override = unk3Override;
                Unk4Override = unk4Override;
                CreatedAtUtc = DateTime.UtcNow;
            }
            public string Version { get; }
            public bool SendUpdateInfo { get; }
            public byte[] PayloadOverride { get; }
            public string PayloadLabel { get; }
            public int? Unk2Override { get; }
            public int? Unk3Override { get; }
            public string Unk4Override { get; }
            public DateTime CreatedAtUtc { get; }
            public int Sends { get; set; }
            public bool IsExpired(DateTime now)
            {
                return Sends >= CollectBookForcedAckLimit || (now - CreatedAtUtc).TotalSeconds >= 15;
            }
        }
        private static bool HasCollectBookItem(
            IReadOnlyCollection<(int id, byte color)> owned,
            int itemId,
            byte color)
        {
            if (owned.Any(i => i.id == itemId && i.color == color))
                return true;
            return color == 0 && owned.Any(i => i.id == itemId);
        }
        private static int[] BuildCollectBookSlotValues(
            IReadOnlyCollection<(int id, byte color)> owned,
            IReadOnlyList<(int itemId, byte color)> slots)
        {
            return slots
                .Take(6)
                .Select(item => HasCollectBookItem(owned, item.itemId, item.color) ? item.itemId : 0)
                .Concat(Enumerable.Repeat(0, 6))
                .Take(6)
                .ToArray();
        }
        private static int BuildCollectBookState(Player player, uint bookKey, IReadOnlyList<int> slotValues)
        {
            if (slotValues == null || slotValues.Take(6).Any(x => x == 0))
                return 0;
            var book = FindCollectBookDefinition(bookKey);
            var rewards = book != null ? GetUsableCollectBookRewards(book) : Array.Empty<RewardSlot>();
            if (rewards.Length == 0)
                return 2;
            return rewards.Any(reward => HasPlayerCollectBookEffect(player, bookKey, reward.EffectId)) ? 4 : 2;
        }
        private static CollectBook_ItemRegist_Ack BuildCollectBookProgressEntry(
            Player player,
            uint bookKey,
            IReadOnlyCollection<(int id, byte color)> owned,
            IReadOnlyList<(int itemId, byte color)> slots)
        {
            var values = BuildCollectBookSlotValues(owned, slots);
            var state = BuildCollectBookState(player, bookKey, values);
            return new CollectBook_ItemRegist_Ack
            {
                Unk1 = bookKey,
                Unk2 = (int)bookKey,
                Unk3 = state,
                Unk4 = values[0],
                Unk5 = values[1],
                Unk6 = values[2],
                Unk7 = values[3],
                Unk8 = values[4],
                Unk9 = values[5]
            };
        }
        private static int CountCollectedCollectBookSlots(CollectBook_ItemRegist_Ack item)
        {
            if (item == null)
                return 0;
            return new[] { item.Unk4, item.Unk5, item.Unk6, item.Unk7, item.Unk8, item.Unk9 }.Count(x => x > 0);
        }
        public static void InitializeCollectBookCache()
        {
            try
            {
                ReloadCollectBookCache(forceVersionBump: false);
            }
            catch (Exception ex)
            {
            }
        }
        private static CollectBookCacheSnapshot GetCollectBookCache()
        {
            var snapshot = collectCacheRef;
            if (snapshot != null)
                return snapshot;
            lock (collectCacheLock)
            {
                snapshot = collectCacheRef;
                if (snapshot == null)
                    snapshot = ReloadCollectBookCache(forceVersionBump: false);
            }
            return snapshot;
        }
        private static CollectBookCacheSnapshot ReloadCollectBookCache(bool forceVersionBump)
        {
            lock (collectCacheLock)
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    var source = "db";
                    List<CollectBookDefinition> books;
                    try
                    {
                        books = LoadCollectBookDefinitionsFromDb(db);
                    }
                    catch (Exception ex)
                    {
                        books = new List<CollectBookDefinition>();
                    }
                    if (books.Count == 0)
                    {
                        source = "xml";
                        books = LoadCollectBookDefinitionsFromXml();
                    }
                    if (books.Count == 0)
                        throw new InvalidOperationException("No collect book definitions found in DB or XML");
                    var version = forceVersionBump
                        ? DateTime.Now.ToString("yyyyMMddHHmmss")
                        : db.QueryFirstOrDefault<string>("SELECT Version FROM collect_book_meta WHERE Id = 1 LIMIT 1");
                    if (string.IsNullOrWhiteSpace(version))
                        version = CollectBookVersion;
                    if (forceVersionBump)
                    {
                        db.Execute(
                            @"INSERT INTO collect_book_meta (Id, Version, UpdatedAt)
                              VALUES (1, @Version, CURRENT_TIMESTAMP)
                              ON DUPLICATE KEY UPDATE
                                Version = VALUES(Version),
                                UpdatedAt = CURRENT_TIMESTAMP;",
                            new { Version = version });
                        db.Execute("DELETE FROM collect_book_meta WHERE Id <> 1");
                    }
                    var snapshot = new CollectBookCacheSnapshot(
                        version,
                        books,
                        BuildCollectBookSlotMap(books));
                    collectCacheRef = snapshot;
                    CollectBookVersion = snapshot.Version;
                    return snapshot;
                }
            }
        }
        private static void EnsureCollectBookPersistenceTables(System.Data.IDbConnection db)
        {
            db.Execute(@"
CREATE TABLE IF NOT EXISTS player_collect_books (
  PlayerId INTEGER NOT NULL,
  BookKey INTEGER NOT NULL,
  IsCompleted INTEGER NOT NULL DEFAULT 0,
  UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (PlayerId, BookKey)
);");
            db.Execute(@"
CREATE TABLE IF NOT EXISTS player_collect_book_slots (
  PlayerId INTEGER NOT NULL,
  BookKey INTEGER NOT NULL,
  Slot INTEGER NOT NULL,
  IsCollected INTEGER NOT NULL DEFAULT 0,
  PRIMARY KEY (PlayerId, BookKey, Slot)
);");
            db.Execute(@"
CREATE TABLE IF NOT EXISTS player_collect_book_effects (
  PlayerId INTEGER NOT NULL,
  BookKey INTEGER NOT NULL,
  EffectId BIGINT NOT NULL,
  RewardType TEXT NOT NULL DEFAULT 'EFFECT',
  IsActive INTEGER NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (PlayerId, BookKey, EffectId)
);");
            try
            {
                db.Execute("ALTER TABLE player_collect_book_effects MODIFY COLUMN EffectId BIGINT NOT NULL");
            }
            catch
            {
            }
        }
        private static List<CollectBookDefinition> LoadCollectBookDefinitionsFromDb(System.Data.IDbConnection db)
        {
            var bookRows = db.Query<CollectBookBookRow>(
                @"SELECT BookKey, Type, Grade, PeriodType, Period, Enabled
                  FROM collect_books
                  WHERE Enabled = 1
                  ORDER BY BookKey").ToList();
            if (bookRows.Count == 0)
                return new List<CollectBookDefinition>();
            var collectRows = db.Query<CollectBookCollectRow>(
                @"SELECT BookKey, Slot, ItemKey, BuyCapsuleKey, Color
                  FROM collect_book_collects
                  ORDER BY BookKey, Slot").ToList();
            var rewardRows = db.Query<CollectBookRewardRow>(
                @"SELECT BookKey, Slot, RewardType, EffectId
                  FROM collect_book_rewards
                  ORDER BY BookKey, Slot").ToList();
            var collectsByBook = collectRows
                .GroupBy(x => x.BookKey)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.Slot)
                        .Select(y => new CollectSlot
                        {
                            Key = (uint)y.ItemKey,
                            BuyCapsuleKey = (uint)y.BuyCapsuleKey,
                            Color = (byte)y.Color
                        })
                        .ToList());
            var rewardsByBook = rewardRows
                .GroupBy(x => x.BookKey)
                .ToDictionary(
                    x => x.Key,
                    x => x.OrderBy(y => y.Slot)
                        .Select(y => new RewardSlot
                        {
                            RewardType = string.IsNullOrWhiteSpace(y.RewardType) ? "NONE" : y.RewardType.Trim(),
                            EffectId = (uint)y.EffectId
                        })
                        .ToList());
            return bookRows
                .Select(row => new CollectBookDefinition
                {
                    Key = (uint)row.BookKey,
                    Type = string.IsNullOrWhiteSpace(row.Type) ? "EQUIP" : row.Type.Trim(),
                    Grade = string.IsNullOrWhiteSpace(row.Grade) ? "NORMAL" : row.Grade.Trim(),
                    PeriodType = string.IsNullOrWhiteSpace(row.PeriodType) ? "DAYS" : row.PeriodType.Trim(),
                    Period = (ushort)row.Period,
                    Collects = collectsByBook.TryGetValue(row.BookKey, out var collects) ? collects : new List<CollectSlot>(),
                    Rewards = rewardsByBook.TryGetValue(row.BookKey, out var rewards) ? rewards : new List<RewardSlot>()
                })
                .OrderBy(x => x.Key)
                .ToList();
        }
        private static List<CollectBookDefinition> LoadCollectBookDefinitionsFromXml()
        {
            var path = FindDecodedCollectBookXml();
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new List<CollectBookDefinition>();
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "collect_book_info", StringComparison.OrdinalIgnoreCase))
                return new List<CollectBookDefinition>();
            return root.Descendants("collect_book")
                .Select(ParseCollectBookDefinition)
                .OrderBy(x => x.Key)
                .ToList();
        }
        private static IReadOnlyDictionary<uint, List<(int itemId, byte color)>> BuildCollectBookSlotMap(IEnumerable<CollectBookDefinition> books)
        {
            return books.ToDictionary(
                x => x.Key,
                x => x.Collects
                    .Take(6)
                    .Select(slot => ((int)slot.Key, slot.Color))
                    .ToList());
        }
        internal static CollectBook_ItemRegist_Ack[] BuildCollectBookInventoryProgress(Player player, bool includeEmpty = false, bool persist = false)
        {
            if (player == null)
                return Array.Empty<CollectBook_ItemRegist_Ack>();
            var cache = GetCollectBookCache();
            var owned = player.Inventory
                .Select(i => (id: (int)i.ItemNumber.Id, color: (byte)i.Color))
                .Distinct()
                .ToList();
            var items = cache.SlotMap
                .OrderBy(x => x.Key)
                .Select(entry => BuildCollectBookProgressEntry(player, entry.Key, owned, entry.Value))
                .ToArray();
            if (persist)
                SavePlayerCollectBookProgress(player, items);
            return items
                .Where(item => includeEmpty || CountCollectedCollectBookSlots(item) > 0)
                .ToArray();
        }
        public static CollectBook_InventoryInfo_Ack CreateCollectBookInventoryInfoAck(Player player, bool includeEmpty = true)
        {
            return new CollectBook_InventoryInfo_Ack
            {
                Items = BuildCollectBookInventoryProgress(player, includeEmpty)
            };
        }
        public static string GetCollectBookVersion()
        {
            return CollectBookVersion;
        }
        public static void InitializeNewShopCache()
        {
            try
            {
                ReloadNewShopCache(forceVersionBump: false);
            }
            catch (Exception ex)
            {
            }
        }
        public static string GetShopVersion()
        {
            return GetNewShopCache().Version ?? string.Empty;
        }
        public static string BumpShopVersion()
        {
            return ReloadNewShopCache(forceVersionBump: true).Version;
        }
        private static string BumpShopVersionString(string currentVersion)
        {
            if (!string.IsNullOrWhiteSpace(currentVersion) &&
                long.TryParse(currentVersion.Trim(), out var numeric))
                return (numeric + 1).ToString();
            return DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        }
        private static NewShopCacheSnapshot GetNewShopCache()
        {
            var snapshot = newShopCacheRef;
            if (snapshot != null)
                return snapshot;
            lock (newShopCacheLock)
            {
                snapshot = newShopCacheRef;
                if (snapshot == null)
                    snapshot = ReloadNewShopCache(forceVersionBump: false);
            }
            return snapshot;
        }
        private static NewShopCacheSnapshot ReloadNewShopCache(bool forceVersionBump)
        {
            lock (newShopCacheLock)
            {
                if (forceVersionBump)
                {
                    using (var db = GameDatabase.Open())
                    {
                        var row = DbUtil.Find<ShopVersionDto>(db).FirstOrDefault();
                        var bumpedVersion = BumpShopVersionString(row?.Version);
                        if (row == null)
                            DbUtil.Insert(db, new ShopVersionDto { Version = bumpedVersion });
                        else
                        {
                            row.Version = bumpedVersion;
                            DbUtil.Update(db, row);
                        }
                    }
                    GameServer.Instance.ResourceCache.Clear(ResourceCacheType.Shop);
                }
                var shop = GameServer.Instance.ResourceCache.GetShop();
                var version = shop.Version ?? string.Empty;
                if (string.IsNullOrWhiteSpace(version))
                    version = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                var itemCount = shop.Items?.Count ?? 0;
                var prices = shop.ShopPrices ?? Array.Empty<byte>();
                var effects = shop.ShopEffects ?? Array.Empty<byte>();
                var items = shop.ShopItems ?? Array.Empty<byte>();
                var uniqueItems = shop.ShopUniqueItems ?? Array.Empty<byte>();
                if (itemCount == 0 || items.Length == 0)
                    throw new InvalidOperationException("New shop cache empty (DB shop_items)");
                var snapshot = new NewShopCacheSnapshot(version, prices, effects, items, uniqueItems, itemCount);
                newShopCacheRef = snapshot;
                return snapshot;
            }
        }
        private static bool WasNewShopUpdateSentRecently(ulong accountId)
        {
            lock (newShopPushClock)
            {
                if (!newShopPushClock.TryGetValue(accountId, out var lastSentUtc))
                    return false;
                return (DateTime.UtcNow - lastSentUtc) <= NewShopDuplicateUpdateWindow;
            }
        }
        private static void MarkNewShopUpdateSent(ulong accountId)
        {
            lock (newShopPushClock)
                newShopPushClock[accountId] = DateTime.UtcNow;
        }
        private static bool NeedsNewShopUpdate(string serverVersion, string clientDate01, string clientDate02, string clientDate03, string clientDate04)
        {
            clientDate01 = (clientDate01 ?? string.Empty).Trim();
            clientDate02 = (clientDate02 ?? string.Empty).Trim();
            clientDate03 = (clientDate03 ?? string.Empty).Trim();
            clientDate04 = (clientDate04 ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clientDate01) ||
                string.IsNullOrWhiteSpace(clientDate02) ||
                string.IsNullOrWhiteSpace(clientDate03) ||
                string.IsNullOrWhiteSpace(clientDate04))
                return true;
            return !string.Equals(clientDate01, serverVersion, StringComparison.Ordinal) ||
                   !string.Equals(clientDate02, serverVersion, StringComparison.Ordinal) ||
                   !string.Equals(clientDate03, serverVersion, StringComparison.Ordinal) ||
                   !string.Equals(clientDate04, serverVersion, StringComparison.Ordinal);
        }
        private static CollectBook_ItemRegist_Ack[] BuildCollectBookProgressForRequest(Player player, CollectBookItem[] requestItems)
        {
            if (player == null || requestItems == null || requestItems.Length == 0)
                return Array.Empty<CollectBook_ItemRegist_Ack>();
            var cache = GetCollectBookCache();
            var owned = player.Inventory
                .Select(i => (id: (int)i.ItemNumber.Id, color: (byte)i.Color))
                .Distinct()
                .ToList();
            return requestItems
                .GroupBy(x => x.Key)
                .Select(group =>
                {
                    var slots = cache.SlotMap.TryGetValue(group.Key, out var mappedSlots)
                        ? mappedSlots
                        : group
                            .GroupBy(item => new { item.ItemId, item.Color })
                            .Select(item => item.First())
                            .Select(item => (itemId: item.ItemId, color: item.Color))
                            .Take(6)
                            .ToList();
                    return BuildCollectBookProgressEntry(player, group.Key, owned, slots);
                })
                .ToArray();
        }
        private static void SavePlayerCollectBookProgress(Player player, IReadOnlyCollection<CollectBook_ItemRegist_Ack> items)
        {
            if (player?.Account == null || items == null)
                return;
            try
            {
                using (var db = GameDatabase.Open())
                using (var tx = db.BeginTransaction())
                {
                    var playerId = (int)player.Account.Id;
                    db.Execute("DELETE FROM player_collect_book_slots WHERE PlayerId = @PlayerId", new { PlayerId = playerId }, tx);
                    db.Execute("DELETE FROM player_collect_books WHERE PlayerId = @PlayerId", new { PlayerId = playerId }, tx);
                    foreach (var item in items)
                    {
                        var flags = new[] { item.Unk4, item.Unk5, item.Unk6, item.Unk7, item.Unk8, item.Unk9 };
                        var bookKey = (long)item.Unk1;
                        var isCompleted = flags.All(x => x > 0) ? 1 : 0;
                        db.Execute(
                            @"INSERT INTO player_collect_books (PlayerId, BookKey, IsCompleted, UpdatedAt)
                              VALUES (@PlayerId, @BookKey, @IsCompleted, @UpdatedAt)",
                            new
                            {
                                PlayerId = playerId,
                                BookKey = bookKey,
                                IsCompleted = isCompleted,
                                UpdatedAt = DateTime.Now
                            },
                            tx);
                        for (var slot = 0; slot < flags.Length; slot++)
                        {
                            db.Execute(
                                @"INSERT INTO player_collect_book_slots (PlayerId, BookKey, Slot, IsCollected)
                                  VALUES (@PlayerId, @BookKey, @Slot, @IsCollected)",
                                new
                                {
                                    PlayerId = playerId,
                                    BookKey = bookKey,
                                    Slot = slot + 1,
                                    IsCollected = flags[slot] > 0 ? 1 : 0
                                },
                                tx);
                        }
                    }
                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
            }
        }
        private static bool IsCollectBookCompleted(Player player, uint bookKey)
        {
            var progress = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false);
            var item = progress.FirstOrDefault(x => x.Unk1 == bookKey);
            if (item == null)
                return false;
            return item.Unk4 > 0 &&
                   item.Unk5 > 0 &&
                   item.Unk6 > 0 &&
                   item.Unk7 > 0 &&
                   item.Unk8 > 0 &&
                   item.Unk9 > 0;
        }
        internal static CollectBookDefinition FindCollectBookDefinition(uint bookKey)
        {
            return GetCollectBookCache().Books.FirstOrDefault(x => x.Key == bookKey);
        }
        private static RewardSlot[] GetUsableCollectBookRewards(CollectBookDefinition book)
        {
            if (book?.Rewards == null)
                return Array.Empty<RewardSlot>();
            return book.Rewards
                .Where(reward => reward.EffectId != 0 &&
                                 !string.Equals(reward.RewardType, "NONE", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
        internal static RewardSlot? GetPrimaryCollectBookReward(CollectBookDefinition book)
        {
            return GetUsableCollectBookRewards(book).FirstOrDefault();
        }
        private static float GetCollectBookPenRateForEffect(uint effectId)
        {
            switch (effectId)
            {
                case 1999801004:
                    return 0.20f;
                case 1999801005:
                    return 0.30f;
                default:
                    return 0f;
            }
        }
        private static float GetCollectBookExpRateForEffect(uint effectId)
        {
            switch (effectId)
            {
                case 1999800003:
                    return 0.10f;
                case 1999800007:
                    return 0.20f;
                default:
                    return 0f;
            }
        }
        private static uint ResolveCollectBookRewardBookKey(ulong raw)
        {
            var direct = (uint)raw;
            if (FindCollectBookDefinition(direct) != null)
                return direct;
            var high = (uint)(raw >> 32);
            if (FindCollectBookDefinition(high) != null)
                return high;
            return direct;
        }
        internal static CollectBookEffectItem BuildCollectBookEffectItem(uint bookKey, RewardSlot reward, bool isActive = true)
        {
            return new CollectBookEffectItem
            {
                Unk1 = (byte)(isActive ? 1 : 0),
                Unk2 = (int)bookKey,
                Unk3 = 0,
                Unk4 = (int)reward.EffectId,
                Unk5 = 0,
                Unk6 = 0,
                Unk7 = reward.RewardType ?? "EFFECT",
                Unk8 = reward.EffectId.ToString(),
                Unk9 = string.Empty,
                Unk10 = string.Empty,
                Unk11 = string.Empty
            };
        }
        internal static PlayerCollectBookEffectRow[] LoadPlayerCollectBookEffectRows(Player player, bool activeOnly = false)
        {
            if (player?.Account == null)
                return Array.Empty<PlayerCollectBookEffectRow>();
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    var sql = @"SELECT PlayerId, BookKey, EffectId, RewardType, IsActive, CreatedAt, UpdatedAt
                                FROM player_collect_book_effects
                                WHERE PlayerId = @PlayerId";
                    if (activeOnly)
                        sql += " AND IsActive = 1";
                    sql += " ORDER BY UpdatedAt DESC, BookKey DESC, EffectId DESC";
                    return db.Query<PlayerCollectBookEffectRow>(
                        sql,
                        new { PlayerId = (int)player.Account.Id }).ToArray();
                }
            }
            catch (Exception ex)
            {
                return Array.Empty<PlayerCollectBookEffectRow>();
            }
        }
        internal static CollectBookEffectItem[] LoadPlayerCollectBookEffects(Player player)
        {
            return LoadPlayerCollectBookEffectRows(player)
                .Select(x => BuildCollectBookEffectItem(
                    (uint)x.BookKey,
                    new RewardSlot
                    {
                        EffectId = (uint)x.EffectId,
                        RewardType = string.IsNullOrWhiteSpace(x.RewardType) ? "EFFECT" : x.RewardType.Trim()
                    },
                    x.IsActive > 0))
                .ToArray();
        }
        private static bool HasPlayerCollectBookEffect(Player player, uint bookKey, uint effectId)
        {
            if (player?.Account == null)
                return false;
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    return db.QueryFirstOrDefault<int>(
                               @"SELECT COUNT(*)
                                 FROM player_collect_book_effects
                                 WHERE PlayerId = @PlayerId
                                   AND BookKey = @BookKey
                                   AND EffectId = @EffectId
                                   AND IsActive = 1",
                               new
                               {
                                   PlayerId = (int)player.Account.Id,
                                   BookKey = (int)bookKey,
                                   EffectId = (long)effectId
                               }) > 0;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private static bool SavePlayerCollectBookEffect(Player player, uint bookKey, RewardSlot reward)
        {
            if (player?.Account == null)
                return false;
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    db.Execute(
                        @"INSERT INTO player_collect_book_effects
                            (PlayerId, BookKey, EffectId, RewardType, IsActive, CreatedAt, UpdatedAt)
                          VALUES
                            (@PlayerId, @BookKey, @EffectId, @RewardType, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                          ON DUPLICATE KEY UPDATE
                            RewardType = VALUES(RewardType),
                            IsActive = 1,
                            UpdatedAt = CURRENT_TIMESTAMP",
                        new
                        {
                            PlayerId = (int)player.Account.Id,
                            BookKey = (int)bookKey,
                            EffectId = (long)reward.EffectId,
                            RewardType = string.IsNullOrWhiteSpace(reward.RewardType) ? "EFFECT" : reward.RewardType.Trim()
                        });
                }
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        private static void DeactivatePlayerCollectBookRewards(Player player, string rewardType, uint? exceptEffectId = null)
        {
            if (player?.Account == null || string.IsNullOrWhiteSpace(rewardType))
                return;
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    db.Execute(
                        @"UPDATE player_collect_book_effects
                          SET IsActive = 0,
                              UpdatedAt = CURRENT_TIMESTAMP
                          WHERE PlayerId = @PlayerId
                            AND RewardType = @RewardType
                            AND (@ExceptEffectId IS NULL OR EffectId <> @ExceptEffectId)",
                        new
                        {
                            PlayerId = (int)player.Account.Id,
                            RewardType = rewardType.Trim(),
                            ExceptEffectId = exceptEffectId.HasValue ? (int?)exceptEffectId.Value : null
                        });
                }
            }
            catch (Exception ex)
            {
            }
        }
        private static void DeactivatePlayerCollectBookOtherBooks(Player player, uint exceptBookKey)
        {
            if (player?.Account == null)
                return;
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    db.Execute(
                        @"UPDATE player_collect_book_effects
                          SET IsActive = 0,
                              UpdatedAt = CURRENT_TIMESTAMP
                          WHERE PlayerId = @PlayerId
                            AND BookKey <> @BookKey",
                        new
                        {
                            PlayerId = (int)player.Account.Id,
                            BookKey = (int)exceptBookKey
                        });
                }
            }
            catch (Exception ex)
            {
            }
        }
        private static bool SetPlayerCollectBookRewardActiveState(Player player, uint bookKey, uint effectId, bool isActive)
        {
            if (player?.Account == null)
                return false;
            try
            {
                using (var db = GameDatabase.Open())
                {
                    EnsureCollectBookPersistenceTables(db);
                    return db.Execute(
                               @"UPDATE player_collect_book_effects
                                 SET IsActive = @IsActive,
                                     UpdatedAt = CURRENT_TIMESTAMP
                                 WHERE PlayerId = @PlayerId
                                   AND BookKey = @BookKey
                                   AND EffectId = @EffectId",
                               new
                               {
                                   IsActive = isActive ? 1 : 0,
                                   PlayerId = (int)player.Account.Id,
                                   BookKey = (int)bookKey,
                                   EffectId = (long)effectId
                               }) > 0;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
        internal static PlayerCollectBookEffectRow FindPlayerCollectBookEffectRow(Player player, uint bookKey, uint effectId, bool activeOnly = false)
        {
            return LoadPlayerCollectBookEffectRows(player, activeOnly)
                .FirstOrDefault(x => x.BookKey == bookKey && x.EffectId == effectId);
        }
        internal static (uint bookKey, uint effectId, string rewardType)? ResolveCollectBookEffectSelection(Player player, ulong value1, ulong value2, uint value3 = 0, bool activeOnly = false)
        {
            var rows = LoadPlayerCollectBookEffectRows(player, activeOnly);
            if (rows.Length == 0)
                return null;
            uint[] candidates =
            {
                (uint)value1,
                (uint)(value1 >> 32),
                (uint)value2,
                (uint)(value2 >> 32),
                value3
            };
            foreach (var row in rows)
            {
                if (candidates.Contains((uint)row.BookKey) && candidates.Contains((uint)row.EffectId))
                    return ((uint)row.BookKey, (uint)row.EffectId, row.RewardType);
            }
            foreach (var row in rows)
            {
                if (candidates.Contains((uint)row.EffectId))
                    return ((uint)row.BookKey, (uint)row.EffectId, row.RewardType);
            }
            foreach (var row in rows)
            {
                if (candidates.Contains((uint)row.BookKey))
                    return ((uint)row.BookKey, (uint)row.EffectId, row.RewardType);
            }
            return null;
        }
        internal static bool ActivatePlayerCollectBookReward(Player player, uint bookKey, uint effectId)
        {
            var row = FindPlayerCollectBookEffectRow(player, bookKey, effectId, activeOnly: false);
            if (row == null)
                return false;
            DeactivatePlayerCollectBookOtherBooks(player, bookKey);
            var sameBookRows = LoadPlayerCollectBookEffectRows(player, activeOnly: false)
                .Where(x => x.BookKey == (int)bookKey)
                .ToArray();
            if (sameBookRows.Length == 0)
                sameBookRows = new[] { row };
            foreach (var sameBookRow in sameBookRows)
            {
                var sameBookRewardType = string.IsNullOrWhiteSpace(sameBookRow.RewardType) ? "EFFECT" : sameBookRow.RewardType.Trim();
                if (string.Equals(sameBookRewardType, "NAMETAGS", StringComparison.OrdinalIgnoreCase))
                    DeactivatePlayerCollectBookRewards(player, "NAMETAGS", (uint)sameBookRow.EffectId);
                if (!SetPlayerCollectBookRewardActiveState(player, (uint)sameBookRow.BookKey, (uint)sameBookRow.EffectId, true))
                    return false;
            }
            RefreshCollectBookRuntimeState(player);
            return true;
        }
        internal static bool DeactivatePlayerCollectBookReward(Player player, uint bookKey, uint effectId)
        {
            if (!SetPlayerCollectBookRewardActiveState(player, bookKey, effectId, false))
                return false;
            RefreshCollectBookRuntimeState(player);
            return true;
        }
        internal static void RefreshCollectBookRuntimeState(Player player)
        {
            if (player == null)
                return;
            var activeRows = LoadPlayerCollectBookEffectRows(player, activeOnly: true);
            var effectNumbers = new List<EffectNumber>();
            var penRate = 0f;
            var expRate = 0f;
            uint nameTag = 0;
            foreach (var row in activeRows)
            {
                var rewardType = string.IsNullOrWhiteSpace(row.RewardType) ? "EFFECT" : row.RewardType.Trim();
                var effectId = (uint)row.EffectId;
                if (string.Equals(rewardType, "NAMETAGS", StringComparison.OrdinalIgnoreCase))
                {
                    if (nameTag == 0)
                        nameTag = effectId;
                    continue;
                }
                if (!string.Equals(rewardType, "EFFECT", StringComparison.OrdinalIgnoreCase))
                    continue;
                effectNumbers.Add(effectId);
                penRate += GetCollectBookPenRateForEffect(effectId);
                expRate += GetCollectBookExpRateForEffect(effectId);
            }
            player.CollectBookEffects = effectNumbers;
            player.CollectBookPenRate = penRate;
            player.CollectBookExpRate = expRate;
            player.CollectBookNameTag = nameTag;
        }
        private static IReadOnlyDictionary<uint, List<(int itemId, byte color)>> LoadCollectBookSlots()
        {
            var result = new Dictionary<uint, List<(int itemId, byte color)>>();
            var path = FindRootCollectBookXml();
            if (path == null)
            {
                return result;
            }
            try
            {
                var doc = XDocument.Load(path);
                foreach (var book in doc.Descendants("collect_book"))
                {
                    if (!uint.TryParse(book.Attribute("key")?.Value, out var bookKey))
                        continue;
                    var slots = book.Elements("collect")
                        .Select(x => new
                        {
                            ItemIdOk = int.TryParse(x.Attribute("key")?.Value, out var itemId),
                            ItemId = itemId,
                            ColorOk = byte.TryParse(x.Attribute("color")?.Value, out var color),
                            Color = color
                        })
                        .Where(x => x.ItemIdOk && x.ColorOk)
                        .Select(x => (x.ItemId, x.Color))
                        .Take(6)
                        .ToList();
                    if (slots.Count > 0)
                        result[bookKey] = slots;
                }
            }
            catch (Exception ex)
            {
            }
            return result;
        }
        private static string FindRootCollectBookXml()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var path = Path.Combine(dir.FullName, "_eu_collect_book.xml");
                    if (File.Exists(path))
                        return path;
                    dir = dir.Parent;
                }
            }
            return null;
        }
        private static string FindDecodedCollectBookXml()
        {
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var path = Path.Combine(dir.FullName, "decoded_collect_from_s4.xml");
                    if (File.Exists(path))
                        return path;
                    dir = dir.Parent;
                }
            }
            return FindRootCollectBookXml();
        }
        private static byte[] RewriteCollectBookXmlVersion(byte[] xml, string version)
        {
            if (xml == null || xml.Length == 0 || string.IsNullOrWhiteSpace(version))
                return xml ?? Array.Empty<byte>();
            var text = Encoding.UTF8.GetString(xml);
            var marker = "version=\"";
            var start = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return xml;
            start += marker.Length;
            var end = text.IndexOf('"', start);
            if (end < 0)
                return xml;
            text = text.Substring(0, start) + version.Trim() + text.Substring(end);
            return Encoding.UTF8.GetBytes(text);
        }
        private static string FindGoodCollectBookS4()
        {
            var candidates = new List<string>();
            foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    candidates.Add(Path.Combine(dir.FullName, "shop", "_eu_collect_book.s4"));
                    candidates.Add(Path.Combine(dir.FullName, "src", "GameServer", "bin", "LatestOld_Release", "shop", "_eu_collect_book.s4"));
                    dir = dir.Parent;
                }
            }
            candidates.Add(@"C:\Users\sneo\Desktop\S4League\shop\_eu_collect_book.s4");
            return candidates
                .Where(File.Exists)
                .Select(path => new FileInfo(path))
                .Where(file => file.Length > 1024)
                .OrderByDescending(file => file.Length)
                .Select(file => file.FullName)
                .FirstOrDefault();
        }
        private static byte[] BuildCollectBookUpdatePayload(string path, out string version, out int bookCount)
        {
            var doc = XDocument.Load(path);
            var root = doc.Root;
            if (root == null || !string.Equals(root.Name.LocalName, "collect_book_info", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("collect_book_info root not found");
            version = root.Attribute("version")?.Value ?? CollectBookVersion;
            var books = root.Descendants("collect_book")
                .Select(ParseCollectBookDefinition)
                .ToList();
            bookCount = books.Count;
            var writer = new CollectBookStreamWriter();
            writer.WriteInt32(books.Count);
            foreach (var book in books)
            {
                writer.WriteInt32((int)book.Key);
                writer.WriteString(book.Type);
                writer.WriteString(book.Grade);
                writer.WriteString(book.PeriodType);
                writer.WriteUInt16(book.Period);
                foreach (var collect in book.Collects.Concat(Enumerable.Repeat(CollectSlot.Empty, 6)).Take(6))
                {
                    writer.WriteInt32((int)collect.Key);
                    writer.WriteInt32((int)collect.BuyCapsuleKey);
                    writer.WriteByte(collect.Color);
                }
                foreach (var reward in book.Rewards.Concat(Enumerable.Repeat(RewardSlot.Empty, 5)).Take(5))
                {
                    writer.WriteString(reward.RewardType);
                    writer.WriteInt32((int)reward.EffectId);
                }
            }
            return writer.ToArray();
        }
        private static byte[] BuildCollectBookUpdatePayload(IReadOnlyCollection<CollectBookDefinition> books, out int bookCount)
        {
            bookCount = books?.Count ?? 0;
            var writer = new CollectBookStreamWriter();
            writer.WriteInt32(bookCount);
            foreach (var book in books ?? Array.Empty<CollectBookDefinition>())
            {
                writer.WriteInt32((int)book.Key);
                writer.WriteString(book.Type);
                writer.WriteString(book.Grade);
                writer.WriteString(book.PeriodType);
                writer.WriteUInt16(book.Period);
                foreach (var collect in (book.Collects ?? new List<CollectSlot>()).Concat(Enumerable.Repeat(CollectSlot.Empty, 6)).Take(6))
                {
                    writer.WriteInt32((int)collect.Key);
                    writer.WriteInt32((int)collect.BuyCapsuleKey);
                    writer.WriteByte(collect.Color);
                }
                foreach (var reward in (book.Rewards ?? new List<RewardSlot>()).Concat(Enumerable.Repeat(RewardSlot.Empty, 5)).Take(5))
                {
                    writer.WriteString(reward.RewardType);
                    writer.WriteInt32((int)reward.EffectId);
                }
            }
            return writer.ToArray();
        }
        private static CollectBookDefinition ParseCollectBookDefinition(XElement book)
        {
            return new CollectBookDefinition
            {
                Key = ReadUInt(book, "key"),
                Type = ReadString(book, "type", "EQUIP"),
                Grade = ReadString(book, "grade", "NORMAL"),
                PeriodType = ReadString(book, "period_type", "DAYS"),
                Period = (ushort)ReadUInt(book, "period"),
                Collects = book.Elements("collect")
                    .Select(x => new CollectSlot
                    {
                        Key = ReadUInt(x, "key"),
                        Color = (byte)ReadUInt(x, "color"),
                        BuyCapsuleKey = ReadUInt(x, "buycapsulekey")
                    })
                    .ToList(),
                Rewards = book.Elements("reward")
                    .Select(x => new RewardSlot
                    {
                        EffectId = ReadUInt(x, "effect_id"),
                        RewardType = ReadString(x, "reward_type", "NONE")
                    })
                    .ToList()
            };
        }
        private static uint ReadUInt(XElement element, string name)
        {
            return uint.TryParse(element.Attribute(name)?.Value, out var value) ? value : 0;
        }
        private static string ReadString(XElement element, string name, string fallback)
        {
            var value = element.Attribute(name)?.Value;
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
        private sealed class CollectBookStreamWriter
        {
            private readonly MemoryStream _stream = new MemoryStream();
            public void WriteInt32(int value) => WriteBytes(BitConverter.GetBytes(value));
            public void WriteUInt16(ushort value) => WriteBytes(BitConverter.GetBytes(value));
            public void WriteByte(byte value) => WriteBytes(new[] { value });
            public void WriteString(string value)
            {
                var bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);
                WriteByte(1);
                WriteCompactInt(bytes.Length);
                if (bytes.Length > 0)
                    WriteBytes(bytes);
            }
            public byte[] ToArray() => _stream.ToArray();
            private void WriteCompactInt(int value)
            {
                if (value >= sbyte.MinValue && value <= sbyte.MaxValue)
                {
                    WriteBytes(new[] { (byte)1, unchecked((byte)value) });
                    return;
                }
                if (value >= short.MinValue && value <= short.MaxValue)
                {
                    var bytes = BitConverter.GetBytes((short)value);
                    WriteBytes(new[] { (byte)2, bytes[0], bytes[1] });
                    return;
                }
                var intBytes = BitConverter.GetBytes(value);
                WriteBytes(new[] { (byte)4, intBytes[0], intBytes[1], intBytes[2], intBytes[3] });
            }
            private void WriteBytes(byte[] bytes)
            {
                _stream.Write(bytes, 0, bytes.Length);
            }
        }
        internal sealed class CollectBookDefinition
        {
            public uint Key { get; set; }
            public string Type { get; set; }
            public string Grade { get; set; }
            public string PeriodType { get; set; }
            public ushort Period { get; set; }
            public List<CollectSlot> Collects { get; set; }
            public List<RewardSlot> Rewards { get; set; }
        }
        internal struct CollectSlot
        {
            public static readonly CollectSlot Empty = new CollectSlot();
            public uint Key { get; set; }
            public uint BuyCapsuleKey { get; set; }
            public byte Color { get; set; }
        }
        internal struct RewardSlot
        {
            public static readonly RewardSlot Empty = new RewardSlot
            {
                RewardType = "NONE"
            };
            public uint EffectId { get; set; }
            public string RewardType { get; set; }
        }
        private sealed class CollectBookCacheSnapshot
        {
            public CollectBookCacheSnapshot(
                string version,
                IReadOnlyList<CollectBookDefinition> books,
                IReadOnlyDictionary<uint, List<(int itemId, byte color)>> slotMap)
            {
                Version = version;
                Books = books;
                SlotMap = slotMap;
            }
            public string Version { get; }
            public IReadOnlyList<CollectBookDefinition> Books { get; }
            public IReadOnlyDictionary<uint, List<(int itemId, byte color)>> SlotMap { get; }
        }
        private sealed class CollectBookBookRow
        {
            public long BookKey { get; set; }
            public string Type { get; set; }
            public string Grade { get; set; }
            public string PeriodType { get; set; }
            public int Period { get; set; }
            public int Enabled { get; set; }
        }
        private sealed class CollectBookCollectRow
        {
            public long BookKey { get; set; }
            public int Slot { get; set; }
            public long ItemKey { get; set; }
            public long BuyCapsuleKey { get; set; }
            public int Color { get; set; }
        }
        private sealed class CollectBookRewardRow
        {
            public long BookKey { get; set; }
            public int Slot { get; set; }
            public string RewardType { get; set; }
            public long EffectId { get; set; }
        }
        internal sealed class PlayerCollectBookEffectRow
        {
            public int PlayerId { get; set; }
            public long BookKey { get; set; }
            public long EffectId { get; set; }
            public string RewardType { get; set; }
            public int IsActive { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
        private sealed class NewShopCacheSnapshot
        {
            public NewShopCacheSnapshot(
                string version,
                byte[] shopPrices,
                byte[] shopEffects,
                byte[] shopItems,
                byte[] shopUniqueItems,
                int itemCount)
            {
                Version = version;
                ShopPrices = shopPrices ?? Array.Empty<byte>();
                ShopEffects = shopEffects ?? Array.Empty<byte>();
                ShopItems = shopItems ?? Array.Empty<byte>();
                ShopUniqueItems = shopUniqueItems ?? Array.Empty<byte>();
                ItemCount = itemCount;
            }
            public string Version { get; }
            public byte[] ShopPrices { get; }
            public byte[] ShopEffects { get; }
            public byte[] ShopItems { get; }
            public byte[] ShopUniqueItems { get; }
            public int ItemCount { get; }
        }
        private static byte[] BuildNewShopUpdatePayload(NewShopCacheSnapshot cache, out int itemCount)
        {
            itemCount = cache?.ItemCount ?? 0;
            var payload = cache?.ShopItems ?? Array.Empty<byte>();
            if (itemCount == 0 || payload.Length == 0)
                throw new InvalidOperationException("New shop item cache empty (DB shop_items)");
            return payload;
        }
        internal static byte[] BuildEuNewShopItemUpdatePayload(out string version, out int itemCount)
        {
            var cache = GetNewShopCache();
            version = cache.Version;
            return BuildNewShopUpdatePayload(cache, out itemCount);
        }
        public static async Task ShopUpdateMsg(GameSession session, string versionOverride = null)
        {
            if (session?.Player == null)
                return;
            var shopCache = GetNewShopCache();
            var shopVersion = string.IsNullOrWhiteSpace(versionOverride)
                ? shopCache.Version
                : versionOverride.Trim();
            var itemBlob = BuildNewShopUpdatePayload(shopCache, out var itemTotal);
            await session.SendAsync(new NewShopUpdateCheckAckMessage
            {
                Date01 = shopVersion,
                Date02 = shopVersion,
                Date03 = shopVersion,
                Date04 = shopVersion,
                Unk = 1
            });
            await session.SendAsync(new NewShopUpdataInfoAckMessage
            {
                Type = ShopResourceType.NewShopPrice,
                Data = shopCache.ShopPrices,
                Date = shopVersion
            }, SendOptions.ReliableSecureCompress);
            await session.SendAsync(new NewShopUpdataInfoAckMessage
            {
                Type = ShopResourceType.NewShopEffect,
                Data = shopCache.ShopEffects,
                Date = shopVersion
            }, SendOptions.ReliableSecureCompress);
            await session.SendAsync(new NewShopUpdataInfoAckMessage
            {
                Type = ShopResourceType.NewShopItem,
                Data = itemBlob,
                Date = shopVersion
            }, SendOptions.ReliableSecureCompress);
            await session.SendAsync(new NewShopUpdataInfoAckMessage
            {
                Type = ShopResourceType.NewShopUniqueItem,
                Data = shopCache.ShopUniqueItems,
                Date = shopVersion
            }, SendOptions.ReliableSecureCompress);
            await session.SendAsync(new NewShopUpdateEndAckMessage());
            MarkNewShopUpdateSent((ulong)session.Player.Account.Id);
        }
        public static async Task ShopUpdateMsg(ProudSession session = null, bool broadcast = false)
        {
            if (broadcast)
            {
                foreach (var openSession in GameServer.Instance.Sessions.Values)
                {
                    if (openSession is GameSession playerSession)
                        await ShopUpdateMsg(playerSession);
                }
                return;
            }
            if (session is GameSession single)
                await ShopUpdateMsg(single);
        }
        [MessageHandler(typeof(NewShopUpdateCheckReqMessage))]
        public async Task ShopUpdateCheckHandler(GameSession session, NewShopUpdateCheckReqMessage message)
        {
            if (session == null || message == null)
                return;
            var replyVersion = GetNewShopCache().Version;
            var needsRefresh = NeedsNewShopUpdate(
                replyVersion,
                message.Date01,
                message.Date02,
                message.Date03,
                message.Date04);
            var who = session.Player != null
                ? $"{session.Player.Account.Nickname}({session.Player.Account.Id})"
                : "no-player";
            if (needsRefresh)
            {
                if (session.Player != null &&
                    WasNewShopUpdateSentRecently((ulong)session.Player.Account.Id))
                {
                    await session.SendAsync(new NewShopUpdateCheckAckMessage
                    {
                        Date01 = replyVersion,
                        Date02 = replyVersion,
                        Date03 = replyVersion,
                        Date04 = replyVersion,
                        Unk = 0
                    });
                    return;
                }
                var pushVersion = replyVersion;
                var clientTag = (message.Date01 ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(clientTag) &&
                    CompareCollectBookVersions(replyVersion, clientTag) <= 0)
                {
                    pushVersion = BuildCollectBookForcedSessionVersion(clientTag, replyVersion);
                }
                if (session.Player != null)
                    await ShopUpdateMsg(session, pushVersion);
                else
                    session.UpdateShop = true;
                return;
            }
            await session.SendAsync(new NewShopUpdateCheckAckMessage
            {
                Date01 = replyVersion,
                Date02 = replyVersion,
                Date03 = replyVersion,
                Date04 = replyVersion,
                Unk = 0
            });
        }
        public static async Task RandomShopUpdateMsg(ProudSession session = null, bool broadcast = false)
        {
            if (session == null && broadcast == false)
                return;
            var recipients = new List<ProudSession>();
            if (broadcast)
            {
                foreach (var openSession in GameServer.Instance.Sessions.Values)
                    recipients.Add(openSession);
            }
            else
                recipients.Add(session);
            var rollShop = GameServer.Instance.ResourceCache.GetRandomShop();
            var shopVersion = rollShop.Version;
            var packed = SantanaExtensions.CompressLZO(rollShop.RandomShopItems);
            foreach (var recipient in recipients)
            {
                await recipient.SendAsync(new RandomShopUpdateCheckAckMessage(shopVersion));
                await recipient.SendAsync(new RandomShopUpdateInfoAckMessage
                {
                    Unk = 31,
                    CompressedData = packed,
                    CompressedLength = packed.Length,
                    DecompressedLength = rollShop.RandomShopItems.Length,
                    Version = shopVersion
                }, SendOptions.ReliableSecureCompress);
            }
        }
        [MessageHandler(typeof(RandomShopUpdateCheckReqMessage))]
        public async Task RandomShopUpdateCheckHandler(GameSession session, RandomShopUpdateCheckReqMessage message)
        {
            if (message.Unk2 > 0)
            {
                return;
            }
            var rollShop = GameServer.Instance.ResourceCache.GetRandomShop();
            var shopVersion = rollShop.Version;
            var packed = SantanaExtensions.CompressLZO(rollShop.RandomShopItems);
            await session.SendAsync(new RandomShopUpdateCheckAckMessage(shopVersion));
            if (session.Player != null) {
                await RandomShopUpdateMsg(session, false);
            }
            else {
                await RandomShopUpdateMsg(session, false);
                session.UpdateRandomShop = true;
            }
        }
        [MessageHandler(typeof(RandomShopRollingStartReqMessage))]
        public void RandomShopRollingStart(GameSession session, RandomShopRollingStartReqMessage message)
        {
            if (session?.Player == null || message == null)
            {
                return;
            }
            var rollShop = GameServer.Instance.ResourceCache.GetRandomShop();
            var catalog = GameServer.Instance.ResourceCache.GetShop();
            var buyer = session.Player;
            var rng = new SecureRandom();
            var category = rollShop.GetCategory(message.Category);
            var categoryItems = rollShop.GetItems(message.Category)?.ToList();
            if (category == null || categoryItems == null || categoryItems.Count <= 0)
            {
                session.SendAsync(new RandomShopRollingStartAckMessage { unk = -4 });
                return;
            }
            var coupon = buyer.Inventory.FirstOrDefault(x => x.ItemNumber == 6000001);
            var rolled = categoryItems[rng.Next(categoryItems.Count)];
            if (category.PiceType == "pen" && buyer.PEN < category.Price)
            {
                session.SendAsync(new RandomShopRollingStartAckMessage { unk = -2 });
                return;
            }
            else if (category.PiceType == "ap" && buyer.AP < category.Price)
            {
                session.SendAsync(new RandomShopRollingStartAckMessage { unk = -2 });
                return;
            }
            else if (category.PiceType == "coupon" && (coupon?.Count ?? 0) < category.Price)
            {
                session.SendAsync(new RandomShopRollingStartAckMessage { unk = -2 });
                return;
            }
            var drops = new List<RandomShopItemsDto>();
            drops.Add(RollDrop(rollShop, catalog, rng, rolled));
            var bonusRoll = rng.Next(101);
            if (bonusRoll > 70 && bonusRoll < 80)
                drops.Add(RollDrop(rollShop, catalog, rng, categoryItems[rng.Next(categoryItems.Count)]));
            if (category.PiceType == "pen")
            {
                buyer.PEN -= (uint)category.Price;
            }
            else if (category.PiceType == "ap")
            {
                buyer.AP -= (uint)category.Price;
            }
            else if (category.PiceType == "coupon")
            {
                if (coupon == null)
                {
                    session.SendAsync(new RandomShopRollingStartAckMessage { unk = -2 });
                    return;
                }
                buyer.Inventory.RemoveOrDecreaseCount(coupon, (uint)Math.Max(1, category.Price));
            }
            foreach (var drop in drops)
            {
                var fx = drop.Effect != 0 ? new EffectNumber[] { new EffectNumber(drop.Effect) } : new EffectNumber[] { 0 };
                try
                {
                    switch (drop.ItemPeriodType)
                    {
                        case ItemPeriodType.None:
                            buyer.Inventory.Create(drop.ItemID, (ushort)drop.Period, drop.Color, fx, 1, 0, true);
                            break;
                        case ItemPeriodType.Days:
                            buyer.Inventory.CreateDays(drop.ItemID, (ushort)drop.Period, drop.Color);
                            break;
                        case ItemPeriodType.Units:
                            buyer.Inventory.CreateUnits(drop.ItemID, drop.Color, fx, drop.Period);
                            break;
                        default:
                            buyer.Inventory.Create(drop.ItemID, (ushort)drop.Period, drop.Color, fx, 1, 0, true);
                            break;
                    }
                }
                catch (Exception ex)
                {
                }
            }
            try
            {
                using (var conn = GameDatabase.Open())
                    buyer.Inventory.Save(conn);
            }
            catch (Exception ex) { }
            session.SendAsync(new RandomShopRollingStartAckMessage { unk = 1, ItemInfo = drops.ToArray() });
            session.SendAsync(new MoneyRefreshCashInfoAckMessage(buyer.PEN, buyer.AP));
        }
        private static RandomShopItemsDto RollDrop(RandomShopResources rollShop, ShopResources catalog, SecureRandom rng, RandomShopItem it)
        {
            var r = rng.Next(100);
            var grade = r < 55 ? 0 : r < 88 ? 10 : 20;
            var box = grade == 0 ? RandomShopBoxColor.Gray : grade == 10 ? RandomShopBoxColor.Blue : RandomShopBoxColor.Orange;
            var pool = rollShop.GetEffectPool(it.EffectGroup);
            var tier = pool.Where(e => e.Grade == grade).ToList();
            uint effect = 0;
            if (grade == 0)
            {
                if (tier.Count > 0 && rng.Next(2) == 0)
                    effect = tier[rng.Next(tier.Count)].Effect;
            }
            else
            {
                var usable = tier.Count > 0 ? tier : pool;
                if (usable.Count > 0)
                    effect = usable[rng.Next(usable.Count)].Effect;
            }
            var periods = rollShop.GetPeriodPool(it.PeriodGroup);
            var weighted = new List<(int Period, int Type)>();
            foreach (var p in periods)
            {
                var isUnlimited = p.Type == 1;
                var weight = isUnlimited ? 1 : 15;
                for (var i = 0; i < weight; i++)
                    weighted.Add(p);
            }
            var per = weighted.Count > 0 ? weighted[rng.Next(weighted.Count)] : (Period: (int)it.Period, Type: (int)it.ItemPeriodType);
            var nColors = Math.Max(1, catalog.GetItem(it.ShopItemId)?.ColorGroup ?? 1);
            var defColor = (int)it.Color;
            var color = (byte)((defColor >= 0 && defColor < nColors) ? defColor : 0);
            return new RandomShopItemsDto
            {
                ItemID = it.ShopItemId,
                ItemPeriodType = (ItemPeriodType)per.Type,
                Period = (uint)per.Period,
                Color = color,
                Effect = effect,
                BoxColor = box,
                Unk = 0
            };
        }
        [MessageHandler(typeof(ShoppingBasketActionReqMessage))]
        public async Task ShoppingBasketActionReq(GameSession session, ShoppingBasketActionReqMessage message)
        {
            if (session?.Player == null || message?.ShopItem == null)
                return;
            Logger.ForAccount(session).Information(
                "[WISHLIST ADD] player={player} unk={unk} item={item} priceType={priceType} periodType={periodType} period={period} color={color} effect={effect}",
                session.Player.Account.Nickname,
                message.Unk,
                (uint)message.ShopItem.ItemNumber,
                (int)message.ShopItem.PriceType,
                (int)message.ShopItem.PeriodType,
                message.ShopItem.Period,
                message.ShopItem.Color,
                message.ShopItem.Effect);
            var addedRow = session.Player.ShoppingBasketManager.Add(message.ShopItem);
            if (addedRow == null)
            {
                await session.SendAsync(new ShoppingBasketActionAckMessage(-1, 0, new ShoppingBasketDto()));
                return;
            }
            await session.SendAsync(new ShoppingBasketActionAckMessage(3, 1, addedRow));
            await session.SendAsync(new ShoppingBasketListInfoAckMessage(session.Player.ShoppingBasketManager.ToArray()));
        }
        [MessageHandler(typeof(ShoppingBasketDeleteReqMessage))]
        public async Task ShoppingBasketDeleteReq(GameSession session, ShoppingBasketDeleteReqMessage message)
        {
            if (session?.Player == null || message?.Unk == null)
                return;
            Logger.ForAccount(session).Information(
                "[WISHLIST DELETE] player={player} ids={ids}",
                session.Player.Account.Nickname,
                string.Join(",", message.Unk));
            var targetIds = message.Unk
                .Where(x => x > 0)
                .Distinct()
                .ToArray();
            var firstTargetId = targetIds.FirstOrDefault();
            var removedRows = targetIds
                .Select(x => session.Player.ShoppingBasketManager[(ulong)x])
                .Where(x => x != null)
                .ToArray();
            var firstRemovedRow = removedRows.FirstOrDefault();
            session.Player.ShoppingBasketManager.Remove(message.Unk);
            var remainingRows = session.Player.ShoppingBasketManager.ToArray();
            Logger.ForAccount(session).Information(
                "[WISHLIST DELETE ACK] player={player} deletedId={deletedId} found={found} remaining={remaining}",
                session.Player.Account.Nickname,
                firstTargetId,
                removedRows.Length > 0,
                remainingRows.Length);
            await session.SendAsync(new ShoppingBasketActionAckMessage(1, 1, firstRemovedRow ?? new ShoppingBasketDto()));
            if (removedRows.Length > 0)
            {
                Logger.ForAccount(session).Information(
                    "[WISHLIST DELETE UI] player={player} deletedIds={deletedIds} sendVisibleRemoveAck=True",
                    session.Player.Account.Nickname,
                    string.Join(",", targetIds));
                foreach (var removedRow in removedRows)
                    await session.SendAsync(new ShoppingBasketActionAckMessage(3, 1, removedRow));
            }
            await session.SendAsync(new ShoppingBasketListInfoAckMessage(remainingRows));
        }
        public static async Task CollectBookUpdateMsg(
            GameSession session,
            string versionOverride = null,
            byte[] payloadOverride = null,
            string payloadLabel = null,
            int? unk2Override = null,
            int? unk3Override = null,
            string unk4Override = null)
        {
            if (session?.Player == null)
                return;
            var cache = GetCollectBookCache();
            var version = string.IsNullOrWhiteSpace(versionOverride) ? CollectBookVersion : versionOverride.Trim();
            var payload = payloadOverride;
            var bookCount = 0;
            var payloadSource = "db-cache";
            if (payload == null)
            {
                try
                {
                    payload = BuildCollectBookUpdatePayload(cache.Books, out bookCount);
                }
                catch (Exception ex)
                {
                    var path = FindDecodedCollectBookXml();
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                        throw;
                    payloadSource = "xml-fallback";
                    payload = RewriteCollectBookXmlVersion(File.ReadAllBytes(path), version);
                    bookCount = cache.Books.Count;
                }
            }
            else
            {
                bookCount = cache.Books.Count;
                payloadSource = payloadLabel ?? "override";
            }
            await session.SendAsync(new CollectBook_UpdateCheck_Ack
            {
                Data = version
            });
            await session.SendAsync(new CollectBook_UpdateInfo_Ack
            {
                Unk1 = payload,
                Unk2 = unk2Override ?? 0,
                Unk3 = unk3Override ?? 0,
                Unk4 = unk4Override ?? version
            });
            var progress = BuildCollectBookInventoryProgress(session.Player, includeEmpty: true, persist: false);
            await session.SendAsync(new CollectBook_InventoryInfo_Ack
            {
                Items = progress
            });
            MarkCollectBookUpdateSent((ulong)session.Player.Account.Id);
        }
        [MessageHandler(typeof(CollectBook_UpdateCheck_Req))]
        public async Task Handle(GameSession s, CollectBook_UpdateCheck_Req m)
        {
            if (s?.Player == null || m == null)
                return;
            var clientVersion = (m.Value ?? string.Empty).Trim();
            CollectBookForcedUpdate forcedUpdate = null;
            var now = DateTime.UtcNow;
            lock (collectPinnedMap)
            {
                if (collectPinnedMap.TryGetValue((ulong)s.Player.Account.Id, out var current))
                {
                    if (current.IsExpired(now))
                        collectPinnedMap.Remove((ulong)s.Player.Account.Id);
                    else
                        forcedUpdate = current;
                }
            }
            var serverVersion = GetCollectBookCache().Version;
            var ackVersion = forcedUpdate?.Version ?? serverVersion;
            var update = !string.Equals(clientVersion, ackVersion, StringComparison.Ordinal);
            if (forcedUpdate?.SendUpdateInfo == true)
            {
                await CollectBookUpdateMsg(
                    s,
                    forcedUpdate.Version,
                    forcedUpdate.PayloadOverride,
                    forcedUpdate.PayloadLabel,
                    forcedUpdate.Unk2Override,
                    forcedUpdate.Unk3Override,
                    forcedUpdate.Unk4Override);
                MarkCollectBookForcedSent((ulong)s.Player.Account.Id, forcedUpdate);
                return;
            }
            if (update)
            {
                if (WasCollectBookUpdateSentRecently((ulong)s.Player.Account.Id))
                {
                    await s.SendAsync(new CollectBook_UpdateCheck_Ack
                    {
                        Data = ackVersion
                    });
                    return;
                }
                var responseVersion = ackVersion;
                if (CompareCollectBookVersions(ackVersion, clientVersion) <= 0)
                {
                    responseVersion = BuildCollectBookForcedSessionVersion(clientVersion, ackVersion);
                }
                await CollectBookUpdateMsg(s, responseVersion);
                return;
            }
            await s.SendAsync(new CollectBook_UpdateCheck_Ack
            {
                Data = ackVersion
            });
            if (forcedUpdate != null)
                MarkCollectBookForcedSent((ulong)s.Player.Account.Id, forcedUpdate);
        }
        [MessageHandler(typeof(CollectBook_InventoryInfo_Req))]
        public async Task HandleInventory(GameSession s, CollectBook_InventoryInfo_Req m)
        {
            var player = s?.Player;
            if (player == null || m == null)
            {
                return;
            }
            var items = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false);
            await s.SendAsync(new CollectBook_InventoryInfo_Ack
            {
                Items = items
            });
        }
        [MessageHandler(typeof(CollectB_ItemRegisterReq))]
        public async Task CollectB_ItemRegisterReq(GameSession s, CollectB_ItemRegisterReq msg)
        {
            var player = s?.Player;
            if (player == null || msg?.Items == null || msg.Items.Length == 0 || msg.Items.Length > 1024)
            {
                return;
            }
            var progress = BuildCollectBookProgressForRequest(player, msg.Items);
            foreach (var item in progress)
                await s.SendAsync(item);
            var fullProgress = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: true);
            await s.SendAsync(new CollectBook_InventoryInfo_Ack
            {
                Items = fullProgress
            });
            var activeInventoryEffects = LoadPlayerCollectBookEffects(player);
            if (activeInventoryEffects.Length > 0)
            {
                await s.SendAsync(new CollectBook_InvenEffectInfo_Ack
                {
                    Items = activeInventoryEffects
                });
            }
            var filledSlots = fullProgress.Sum(CountCollectedCollectBookSlots);
        }
        [MessageHandler(typeof(CollectBook_UseReward_Req))]
        public async Task HandleCollectBookUseReward(GameSession s, CollectBook_UseReward_Req m)
        {
            var player = s?.Player;
            if (player == null || m == null)
            {
                return;
            }
            var bookKey = ResolveCollectBookRewardBookKey(m.Unk1);
            var book = FindCollectBookDefinition(bookKey);
            if (book == null)
            {
                await s.SendAsync(new CollectBook_BookUseReward_Ack
                {
                    Value = 1,
                    Data = new BookUseRewardData()
                });
                return;
            }
            var rewards = GetUsableCollectBookRewards(book);
            if (rewards.Length == 0)
            {
                await s.SendAsync(new CollectBook_BookUseReward_Ack
                {
                    Value = 2,
                    Data = new BookUseRewardData()
                });
                return;
            }
            var primaryReward = rewards[0];
            if (!IsCollectBookCompleted(player, bookKey))
            {
                await s.SendAsync(new CollectBook_BookUseReward_Ack
                {
                    Value = 3,
                    Data = new BookUseRewardData
                    {
                        Unk1 = (int)bookKey,
                        Unk2 = (int)primaryReward.EffectId
                    }
                });
                return;
            }
            var alreadyActive = rewards.All(reward => HasPlayerCollectBookEffect(player, bookKey, reward.EffectId));
            if (alreadyActive)
            {
                DeactivatePlayerCollectBookOtherBooks(player, bookKey);
                ActivatePlayerCollectBookReward(player, bookKey, primaryReward.EffectId);
                var currentProgress = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false)
                    .FirstOrDefault(x => x.Unk1 == bookKey);
                await s.SendAsync(new CollectBook_ResuseBookReward_Ack
                {
                    Value = bookKey
                });
                if (currentProgress != null)
                    await s.SendAsync(currentProgress);
                await s.SendAsync(new CollectBook_InventoryInfo_Ack
                {
                    Items = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false)
                });
                await s.SendAsync(new CollectBook_InvenEffectInfo_Ack
                {
                    Items = LoadPlayerCollectBookEffects(player)
                });
                AuthService.LoadPlayerNameTag(player, true, false);
                return;
            }
            DeactivatePlayerCollectBookOtherBooks(player, bookKey);
            var newlyActivated = new List<RewardSlot>();
            foreach (var reward in rewards)
            {
                if (string.Equals(reward.RewardType, "NAMETAGS", StringComparison.OrdinalIgnoreCase))
                    DeactivatePlayerCollectBookRewards(player, "NAMETAGS", reward.EffectId);
                if (HasPlayerCollectBookEffect(player, bookKey, reward.EffectId))
                    continue;
                if (!SavePlayerCollectBookEffect(player, bookKey, reward))
                {
                    await s.SendAsync(new CollectBook_BookUseReward_Ack
                    {
                        Value = 4,
                        Data = new BookUseRewardData
                        {
                            Unk1 = (int)bookKey,
                            Unk2 = (int)reward.EffectId
                        }
                    });
                    return;
                }
                newlyActivated.Add(reward);
            }
            RefreshCollectBookRuntimeState(player);
            var effectItemAcks = rewards
                .Where(reward => string.Equals(reward.RewardType, "EFFECT", StringComparison.OrdinalIgnoreCase))
                .Select(reward => BuildCollectBookEffectItem(bookKey, reward))
                .ToArray();
            var updatedProgress = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false)
                .FirstOrDefault(x => x.Unk1 == bookKey);
            await s.SendAsync(new CollectBook_BookUseReward_Ack
            {
                Value = 0,
                Data = new BookUseRewardData
                {
                    Unk1 = (int)bookKey,
                    Unk2 = (int)primaryReward.EffectId,
                    Unk3 = book.Period,
                    Unk4 = 0,
                    Unk5 = primaryReward.RewardType ?? "EFFECT",
                    Unk6 = book.Type ?? string.Empty,
                    Unk7 = book.Grade ?? string.Empty
                }
            });
            if (effectItemAcks.Length > 0)
            {
                await s.SendAsync(new CollectBook_EffectRegist_Ack
                {
                    Value = 0,
                    Items = effectItemAcks
                });
            }
            if (updatedProgress != null)
                await s.SendAsync(updatedProgress);
            await s.SendAsync(new CollectBook_InventoryInfo_Ack
            {
                Items = BuildCollectBookInventoryProgress(player, includeEmpty: true, persist: false)
            });
            await s.SendAsync(new CollectBook_InvenEffectInfo_Ack
            {
                Items = LoadPlayerCollectBookEffects(player)
            });
            if (player.CollectBookNameTag > 0)
                AuthService.LoadPlayerNameTag(player, true, false);
        }
        [MessageHandler(typeof(ItemBuyItemReqMessage))]
        public void BuyItemHandler(GameSession session, ItemBuyItemReqMessage message)
        {
            try
            {
                if (session?.Player == null || message?.Items == null || message.Items.Length == 0 || message.Items.Length > 128)
                {
                    session?.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                    return;
                }
                var catalog = GameServer.Instance.ResourceCache.GetShop();
                var buyer = session.Player;
                foreach (var order in message.Items)
                {
                    if (order == null || order.ItemNumber.Id <= 0 || order.Period < 0 || order.Color > 100)
                    {
                        session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                        return;
                    }
                    var catalogEntry = catalog.GetItemInfo(order.ItemNumber, order.PriceType);
                    if (catalogEntry == null)
                    {
                        Logger.ForAccount(session).Error("No shop entry found for {item}", new { order.ItemNumber, order.PriceType, order.Period, order.PeriodType });
                        session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                        return;
                    }
                    if (catalogEntry.ShopInfoType == 0)
                    {
                        Logger.ForAccount(session).Error("Shop entry is not enabled {item}", new { order.ItemNumber, order.PriceType, order.Period, order.PeriodType });
                        session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                        return;
                    }
                    var tierGroup = catalogEntry.PriceGroup;
                    var tier = tierGroup.GetPrice(order.PeriodType, order.Period);
                    if (tier == null || !tier.IsEnabled)
                    {
                        session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                        return;
                    }
                    if (order.Color > catalogEntry.ShopItem.ColorGroup)
                    {
                        session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                        return;
                    }
                    var grantedEffects = new List<EffectNumber>();
                    if (order.Effect != 0)
                    {
                        if (catalogEntry.EffectGroup.MainEffect != order.Effect)
                        {
                            session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.UnkownItem));
                            return;
                        }
                        foreach (var fx in catalogEntry.EffectGroup.Effects)
                            grantedEffects.Add(fx.Effect);
                    }
                    else
                    {
                        grantedEffects.Add(0);
                    }
                    var penBefore = buyer.PEN;
                    var apBefore = buyer.AP;
                    switch (catalogEntry.PriceGroup.PriceType)
                    {
                        case ItemPriceType.PEN:
                            if (buyer.PEN < tier.Price)
                            {
                                session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.NotEnoughMoney));
                                return;
                            }
                            buyer.PEN -= (uint)tier.Price;
                            break;
                        case ItemPriceType.AP:
                        case ItemPriceType.Premium:
                            if (buyer.AP < tier.Price)
                            {
                                session.SendAsync(new ItemBuyItemAckMessage(ItemBuyResult.NotEnoughMoney));
                                return;
                            }
                            buyer.AP -= (uint)tier.Price;
                            break;
                    }
                    PlayerItem mergeTarget = null;
                    var merged = false;
                    switch (order.PeriodType)
                    {
                        case ItemPeriodType.Units:
                            mergeTarget = session.Player.Inventory
                                .FirstOrDefault(x => x.ItemNumber == order.ItemNumber && x.Color == order.Color);
                            if (mergeTarget != null)
                            {
                                mergeTarget.Count += order.Period;
                                mergeTarget.NeedsToSave = true;
                                merged = true;
                                UpdateItemInDB(buyer, mergeTarget);
                            }
                            break;
                        case ItemPeriodType.Days:
                            mergeTarget = session.Player.Inventory
                                .FirstOrDefault(x => x.ItemNumber == order.ItemNumber && x.Color == order.Color);
                            if (mergeTarget != null)
                            {
                                mergeTarget.DaysLeft += order.Period;
                                merged = true;
                                UpdateItemInDB(buyer, mergeTarget);
                            }
                            break;
                        case ItemPeriodType.Hours:
                            mergeTarget = session.Player.Inventory
                                .FirstOrDefault(x => x.ItemNumber == order.ItemNumber && x.Color == order.Color);
                            if (mergeTarget != null)
                            {
                                mergeTarget.HoursLeft += order.Period;
                                mergeTarget.NeedsToSave = true;
                                merged = true;
                                UpdateItemInDB(buyer, mergeTarget);
                            }
                            break;
                    }
                    var boughtItem = mergeTarget;
                    if (!merged)
                    {
                        boughtItem = session.Player.Inventory.Create(
                            catalogEntry,
                            tier,
                            order.Color,
                            grantedEffects.ToArray(),
                            (uint)(tier.PeriodType == ItemPeriodType.Units ? tier.Period : 0)
                        );
                        AddItemInDB(buyer, boughtItem);
                    }
                    else
                    {
                        session.SendAsync(new ItemUpdateInventoryAckMessage(
                            InventoryAction.Update,
                            boughtItem.Map<PlayerItem, ItemDto>()
                        ));
                    }
                    var consumeOutcome = OnBuyAction(buyer, boughtItem);
                    if (consumeOutcome.Item1 && consumeOutcome.Item2)
                        buyer.Inventory.Remove(boughtItem);
                    if (consumeOutcome.Item1 && !consumeOutcome.Item2)
                    {
                        buyer.AP = apBefore;
                        buyer.PEN = penBefore;
                    }
                    session.SendAsync(new ItemBuyItemAckMessage(new[] { boughtItem.Id }, order));
                    session.SendAsync(new MoneyRefreshCashInfoAckMessage(buyer.PEN, buyer.AP));
                    using (var conn = GameDatabase.Open())
                    {
                        buyer.Inventory.Save(conn);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Information(ex.ToString());
            }
        }
        public Tuple<bool, bool> OnBuyAction(Player plr, PlayerItem item)
        {
            switch (item.ItemNumber)
            {
                default:
                    return new Tuple<bool, bool>(false, false);
            }
        }
        public static void UpdateItemInDB(Player plr, PlayerItem item)
        {
            try
            {
                using (var conn = GameDatabase.Open())
                {
                    var effectList = item.Effects.ToList();
                    var effectsCsv = effectList.Count > 0 ? string.Join(",", effectList) : "0";
                    DbUtil.Update(conn, new PlayerItemDto
                    {
                        Id = (int)item.Id,
                        PlayerId = (int)plr.Account.Id,
                        ShopItemInfoId = item.GetShopItemInfo().Id,
                        ShopPriceId = item.GetShopPrice().Id,
                        Period = item.Period,
                        DaysLeft = item.DaysLeft,
                        Effects = effectsCsv,
                        Color = item.Color,
                        PurchaseDate = item.PurchaseDate.ToUnixTimeSeconds(),
                        Durability = item.Durability,
                        Count = (int)item.Count,
                        EnchantMP = item.EnchantMP,
                        EnchantLvl = item.EnchantLvl
                    });
                }
            }
            catch
            {
                item.ExistsInDatabase = false;
            }
        }
        public static void AddItemInDB(Player plr, PlayerItem item)
        {
            try
            {
                using (var conn = GameDatabase.Open())
                {
                    var effectList = item.Effects.ToList();
                    var effectsCsv = effectList.Count > 0 ? string.Join(",", effectList) : "0";
                    DbUtil.Insert(conn, new PlayerItemDto
                    {
                        PlayerId = (int)plr.Account.Id,
                        ShopItemInfoId = item.GetShopItemInfo().Id,
                        ShopPriceId = item.GetShopPrice().Id,
                        Effects = effectsCsv,
                        Color = item.Color,
                        PurchaseDate = DateTimeOffset.Now.ToUnixTimeSeconds(),
                        Durability = item.Durability,
                        Count = (int)item.Count
                    });
                    item.ExistsInDatabase = true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex.ToString());
                item.ExistsInDatabase = false;
            }
        }
        public static void OnPlayerLogout(ulong accountId)
        {
            collectPushClock.Remove(accountId);
            collectRebuildClock.Remove(accountId);
            ClearCollectBookForcedVersion(accountId);
        }
    }
}

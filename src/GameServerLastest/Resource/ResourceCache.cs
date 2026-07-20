using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SantanaLib.Caching;
using Dapper.FastCrud;
using Santana.Database.Auth;
using Santana.Database.Game;
using Santana.Network;
using Santana.Network.Services;
using Serilog;
using Serilog.Core;
namespace Santana.Resource
{
    internal class ResourceCache
    {
        private static readonly ILogger Logger =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ResourceCache));
        private readonly ICache _store = new MemoryCache();
        public readonly ResourceLoader _loader;
        public ResourceCache()
        {
            var dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            _loader = new ResourceLoader(dataPath);
        }
        public void PreCache()
        {
            Logger.Information("Warming up table: item effects");
            GetEffects();
            Logger.Information("Warming up table: item catalog");
            GetItems();
            Logger.Information("Warming up table: starter loadout");
            GetDefaultItems();
            Logger.Information("Warming up table: storefront");
            GetShop();
            ShopService.InitializeNewShopCache();
            Logger.Information("Warming up table: level curve");
            GetExperience();
            Logger.Information("Warming up table: map list");
            GetMaps();
            Logger.Information("Warming up table: match tempos");
            GetGameTempos();
            Logger.Information("Warming up table: capsule rewards");
            GetItemRewards();
            Logger.Information("Warming up table: enchant tiers");
           GetItemEnchant();
            Logger.Information("Warming up table: esper tiers");
            GetEsperEnchant();
            Logger.Information("Warming up table: gacha storefront");
            GetRandomShop();
            Logger.Information("Warming up table: collect book");
            ShopService.InitializeCollectBookCache();
        }
        public IReadOnlyList<ChannelDto> GetChannels()
        {
            var cached = _store.Get<IReadOnlyList<ChannelDto>>(ResourceCacheType.Channels);
            if (cached == null)
            {
                Logger.Information("Warming up table: channel list");

                using (var db = GameDatabase.Open())
                {
                    cached = DbUtil.Find<ChannelDto>(db).ToList();
                }
                _store.Set(ResourceCacheType.Channels, cached);
            }
            return cached;
        }
        public IReadOnlyList<DBClubInfoDto> GetClubs()
        {
            var cached = _store.Get<IReadOnlyList<DBClubInfoDto>>(ResourceCacheType.Clubs);
            if (cached == null)
            {
                Logger.Information("Warming up table: clan roster");

                using (var db = GameDatabase.Open())
                {
                    var allClubs = DbUtil.Find<ClubDto>(db).ToList();
                    var allMembers = DbUtil.Find<ClubPlayerDto>(db).ToList();
                    var assembled = new List<DBClubInfoDto>();
                    foreach (var club in allClubs)
                    {
                        var info = new DBClubInfoDto { ClubDto = club };
                        var members = new List<ClubPlayerInfo>();
                        foreach (var member in allMembers.Where(p => p.ClubId == club.Id))
                        {
                            using (var authDb = AuthDatabase.Open())
                            {
                                var account = DbUtil.Find<AccountDto>(authDb, statement => statement
                                        .Where($"{nameof(AccountDto.Id):C} = @{nameof(member.PlayerId)}")
                                        .WithParameters(new { member.PlayerId }))
                                    .FirstOrDefault();
                                members.Add(new ClubPlayerInfo
                                {
                                    AccountId = (ulong)member.PlayerId,
                                    State = (ClubState)member.State,
                                    Rank = (ClubRank)member.Rank,
                                    Account = account
                                });
                            }
                        }
                        info.PlayerDto = members.ToArray();
                        assembled.Add(info);
                    }
                    cached = assembled.ToArray();
                }
                _store.Set(ResourceCacheType.Clubs, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<uint, ItemEffect> GetEffects()
        {
            var cached = _store.Get<IReadOnlyDictionary<uint, ItemEffect>>(ResourceCacheType.Effects);
            if (cached == null)
            {

                cached = _loader.LoadEffects().ToDictionary(effect => effect.Id);
                _store.Set(ResourceCacheType.Effects, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<ItemNumber, ItemInfo> GetItems()
        {
            var cached = _store.Get<IReadOnlyDictionary<ItemNumber, ItemInfo>>(ResourceCacheType.Items);
            if (cached == null)
            {

                cached = _loader.LoadItems_3().ToDictionary(item => item.ItemNumber);
                _store.Set(ResourceCacheType.Items, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<string, ItemInfo> GetItemsByName()
        {
            var cached = _store.Get<IReadOnlyDictionary<string, ItemInfo>>(ResourceCacheType.Items);
            if (cached == null)
            {

                cached = _loader.LoadItems_4().ToDictionary(item => item.Name);
                _store.Set(ResourceCacheType.Items, cached);
            }
            return cached;
        }
        public IReadOnlyList<DefaultItem> GetDefaultItems()
        {
            var cached = _store.Get<IReadOnlyList<DefaultItem>>(ResourceCacheType.DefaultItems);
            if (cached == null)
            {

                cached = _loader.LoadDefaultItems().ToList();
                _store.Set(ResourceCacheType.DefaultItems, cached);
            }
            return cached;
        }
        public ShopResources GetShop()
        {
            var cached = _store.Get<ShopResources>(ResourceCacheType.Shop);
            if (cached == null)
            {

                cached = new ShopResources();
                _store.Set(ResourceCacheType.Shop, cached);
            }
            if (string.IsNullOrWhiteSpace(cached.Version))
                cached.Load();
            return cached;
        }
        public RandomShopResources GetRandomShop()
        {
            var cached = _store.Get<RandomShopResources>(ResourceCacheType.RandomShop);
            if (cached == null)
            {

                cached = new RandomShopResources();
                _store.Set(ResourceCacheType.RandomShop, cached);
            }
            if (string.IsNullOrWhiteSpace(cached.Version))
                cached.Load();
            return cached;
        }
        public IReadOnlyDictionary<int, Experience> GetExperience()
        {
            var cached = _store.Get<IReadOnlyDictionary<int, Experience>>(ResourceCacheType.Exp);
            if (cached == null)
            {

                cached = _loader.LoadExperience().ToDictionary(e => e.Level);
                _store.Set(ResourceCacheType.Exp, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<int, MapInfo> GetMaps()
        {
            var cached = _store.Get<IReadOnlyDictionary<int, MapInfo>>(ResourceCacheType.Maps);
            if (cached == null)
            {

                cached = _loader.LoadMaps().ToDictionary(map => map.Id);
                _store.Set(ResourceCacheType.Maps, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<string, GameTempo> GetGameTempos()
        {
            var cached = _store.Get<IReadOnlyDictionary<string, GameTempo>>(ResourceCacheType.GameTempo);
            if (cached == null)
            {

                cached = _loader.LoadGameTempos().ToDictionary(t => t.Name);
                _store.Set(ResourceCacheType.GameTempo, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<ItemNumber, AddCapsule> GetCapsules()
        {
            var cached = _store.Get<IReadOnlyDictionary<ItemNumber, AddCapsule>>(ResourceCacheType.Capsules);
            if (cached == null)
            {

                cached = _loader.LoadCapsules().ToDictionary(t => t.CapsuleItemId);
                _store.Set(ResourceCacheType.Capsules, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<ulong, CapsuleRewards> GetItemRewards()
        {
            var cached = _store.Get<IReadOnlyDictionary<ulong, CapsuleRewards>>(ResourceCacheType.ItemRewards);
            if (cached == null)
            {

                cached = _loader.LoadItemRewards().ToDictionary(t => (ulong)t.Item);
                _store.Set(ResourceCacheType.ItemRewards, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<uint, ItemEnchant> GetItemEnchant()
        {
            var cached = _store.Get<IReadOnlyDictionary<uint, ItemEnchant>>(ResourceCacheType.Enchant);
            if (cached == null)
            {

                cached = _loader.LoadItemEnchant().ToDictionary(t => (uint)t.Id);
                _store.Set(ResourceCacheType.Enchant, cached);
            }
            return cached;
        }
        public IReadOnlyDictionary<uint, EsperEnchant> GetEsperEnchant()
        {
            var cached = _store.Get<IReadOnlyDictionary<uint, EsperEnchant>>(ResourceCacheType.EsperEnchant);
            if (cached == null)
            {

                cached = _loader.LoadEsperEnchant().ToDictionary(t => (uint)t.Level);
                _store.Set(ResourceCacheType.EsperEnchant, cached);
            }
            return cached;
        }
        public void Clear()
        {
            Logger.Information("Emptying all cached resources");
            _store.Clear();
        }
        public void Clear(ResourceCacheType type)
        {
            Logger.Information($"Dropping cached {type} so it reloads next time");
            if (type == ResourceCacheType.Shop)
            {
                GetShop().Clear();
                return;
            }
            _store.Remove(type.ToString());
        }
    }
    internal static class ResourceCacheExtensions
    {
        public static T Get<T>(this ICache cache, ResourceCacheType type)
            where T : class
        {
            return cache.Get<T>(type.ToString());
        }
        public static void Set(this ICache cache, ResourceCacheType type, object value)
        {
            cache.Set(type.ToString(), value);
        }
        public static void Set(this ICache cache, ResourceCacheType type, object value, TimeSpan ts)
        {
            cache.Set(type.ToString(), value, ts);
        }
    }
}

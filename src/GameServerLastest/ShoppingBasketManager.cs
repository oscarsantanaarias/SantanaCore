using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Dapper.FastCrud;
using Santana.Database.Game;
using ShoppingBasketNetDto = Santana.Network.Data.Game.ShoppingBasketDto;
using ShopItemNetDto = Santana.Network.Data.Game.ShopItemDto;

namespace Santana
{
    internal class ShoppingBasketManager : IReadOnlyCollection<ShoppingBasketNetDto>
    {
        private readonly ConcurrentDictionary<ulong, ShoppingBasketNetDto> _basketEntries =
            new ConcurrentDictionary<ulong, ShoppingBasketNetDto>();

        public ShoppingBasketManager(Player plr, PlayerDto dto)
        {
            Player = plr;

            using (var conn = GameDatabase.Open())
            {
                var storedRows = DbUtil.Find<ShoppingBasketItemDto>(conn, statement => statement
                    .Where($"{nameof(ShoppingBasketItemDto.PlayerId):C} = @playerId")
                    .WithParameters(new { playerId = (int)plr.Account.Id }))
                    .ToArray();

                foreach (var storedRow in storedRows)
                    _basketEntries.TryAdd((ulong)storedRow.Id, WrapRow(storedRow));
            }
        }

        public Player Player { get; }
        public int Count => _basketEntries.Count;
        public ShoppingBasketNetDto this[ulong id] => CollectionExtensions.GetValueOrDefault(_basketEntries, id);

        public IEnumerator<ShoppingBasketNetDto> GetEnumerator()
        {
            return _basketEntries.Values.OrderBy(x => x.ItemId).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public ShoppingBasketNetDto[] ToArray()
        {
            return _basketEntries.Values.OrderBy(x => x.ItemId).ToArray();
        }

        public ShoppingBasketNetDto Add(ShopItemNetDto shopItem)
        {
            if (shopItem == null)
                return null;

            var duplicate = _basketEntries.Values.FirstOrDefault(x =>
                x.ShopItem.ItemNumber == shopItem.ItemNumber &&
                x.ShopItem.PriceType == shopItem.PriceType &&
                x.ShopItem.PeriodType == shopItem.PeriodType &&
                x.ShopItem.Period == shopItem.Period &&
                x.ShopItem.Color == shopItem.Color &&
                x.ShopItem.Effect == shopItem.Effect);

            if (duplicate != null)
                return duplicate;

            var newRow = new ShoppingBasketItemDto
            {
                PlayerId = (int)Player.Account.Id,
                ItemId = (int)(uint)shopItem.ItemNumber,
                Effects = shopItem.Effect,
                Color = shopItem.Color,
                Period = shopItem.Period,
                PeriodType = (int)shopItem.PeriodType,
                PriceType = (int)shopItem.PriceType
            };

            using (var conn = GameDatabase.Open())
                DbUtil.Insert(conn, newRow);

            var netDto = WrapRow(newRow);
            _basketEntries[(ulong)newRow.Id] = netDto;
            return netDto;
        }

        public bool Remove(IEnumerable<long> ids)
        {
            var anyRemoved = false;

            foreach (var id in ids ?? Enumerable.Empty<long>())
            {
                if (!_basketEntries.TryRemove((ulong)id, out _))
                    continue;

                anyRemoved = true;

                using (var conn = GameDatabase.Open())
                    DbUtil.Delete(conn, new ShoppingBasketItemDto { Id = (int)id });
            }

            return anyRemoved;
        }

        private static ShoppingBasketNetDto WrapRow(ShoppingBasketItemDto source)
        {
            return new ShoppingBasketNetDto
            {
                ItemId = (ulong)source.Id,
                ShopItem = new ShopItemNetDto
                {
                    ItemNumber = (uint)source.ItemId,
                    Effect = source.Effects,
                    Color = source.Color,
                    Period = source.Period,
                    PeriodType = (ItemPeriodType)source.PeriodType,
                    PriceType = (ItemPriceType)source.PriceType
                }
            };
        }
    }
}

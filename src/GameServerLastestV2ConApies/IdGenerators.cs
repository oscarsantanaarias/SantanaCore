using System.Linq;
using System.Threading;
using Dapper.FastCrud;
using Santana.Database.Game;

namespace Santana
{
    internal static class ItemIdGenerator
    {
        private static long _lastItemId;

        public static void Initialize()
        {
            using (var conn = GameDatabase.Open())
            {
                var storedItems = DbUtil.Find<PlayerItemDto>(conn);
                if (storedItems.Any())
                    _lastItemId = storedItems.Max(row => row.Id);
            }
        }

        public static ulong GetNextId()
        {
            using (var conn = GameDatabase.Open())
            {
                var takenNext = DbUtil.Find<PlayerItemDto>(conn).Where(row => row.Id == _lastItemId + 1);

                if (takenNext == null)
                    return (ulong)Interlocked.Add(ref _lastItemId, 1);

                return (ulong)Interlocked.Add(ref _lastItemId, takenNext.Count() + 1);
            }
        }
    }

    internal static class DenyIdGenerator
    {
        private static int _lastDenyId;

        public static void Initialize()
        {
            using (var conn = GameDatabase.Open())
            {
                var storedDenies = DbUtil.Find<PlayerDenyDto>(conn);
                if (storedDenies.Any())
                    _lastDenyId = storedDenies.Max(row => row.Id);
            }
        }

        public static int GetNextId()
        {
            return Interlocked.Add(ref _lastDenyId, 1);
        }
    }
}

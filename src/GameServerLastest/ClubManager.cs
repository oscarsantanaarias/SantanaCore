using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SantanaLib.Threading.Tasks;

namespace Santana
{
    internal class ClubManager : IReadOnlyCollection<Club>
    {
        private readonly ConcurrentDictionary<uint, Club> _byId = new ConcurrentDictionary<uint, Club>();
        internal readonly AsyncLock _gate = new AsyncLock();

        public ClubManager(IEnumerable<DBClubInfoDto> clubInfos)
        {
            _byId.Clear();
            foreach (var dto in clubInfos)
            {
                var loaded = new Club(dto.ClubDto, dto.PlayerDto);
                _byId.TryAdd(dto.ClubDto.Id, loaded);
            }
        }

        public Club this[uint id] => GetClub(id);

        public Club GetClub(uint id)
        {
            {
                Club found;
                _byId.TryGetValue(id, out found);
                return found;
            }
        }

        public Club GetClubByAccount(ulong id)
        {
            {
                return _byId.Values.FirstOrDefault(c => c.Players.Any(p => p.Value.AccountId == id));
            }
        }

        public void UpdateClubWarStats(uint clubId, uint rank, uint points, uint win, uint loss)
        {
            if (_byId.TryGetValue(clubId, out var target))
                target.ApplyClubWarStats(rank, points, win, loss);
        }

        public void Remove(Club club)
        {
            {
                if (club == null)
                    return;
                _byId.TryRemove(club.Id, out var _);
            }
        }

        public void Add(Club club)
        {
            {
                _byId.TryAdd(club.Id, club);
            }
        }

        #region IReadOnlyCollection

        public int Count => _byId.Count;

        public IEnumerator<Club> GetEnumerator()
        {
            {
                return _byId.Values.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            {
                return GetEnumerator();
            }
        }

        #endregion
    }
}

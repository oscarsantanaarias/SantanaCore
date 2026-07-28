using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Santana
{
    internal class AchieveMissionManager
    {
        readonly Player _owner;

        public int TD;
        public int TDAssist;
        public int Kills;
        public int KillsAssist;
        public int ChaserKills;
        public int ChaserSelected;
        public int BRFirst;

        public AchieveMissionManager(Player player)
        {
            _owner = player;
        }

        public void AchieveMissionReset()
        {
            TD = 0;
            TDAssist = 0;
            Kills = 0;
            KillsAssist = 0;
            ChaserKills = 0;
            ChaserSelected = 0;
            BRFirst = 0;
        }

        public void AchieveMissionInfo()
        {
            using (var connection = GameDatabase.Open())
            {
                var row = DbUtil.Find<AchieveMissionDto>(connection, statement => statement
                    .Where($"{nameof(AchieveMissionDto.PlayerId):C} = @{nameof(_owner.Account.Id)}")
                    .WithParameters(new { _owner.Account.Id })).FirstOrDefault();

                if (row == null)
                {
                    var fresh = new AchieveMissionDto();
                    fresh.PlayerId = _owner.Account.Id;
                    DbUtil.Insert(connection, fresh);
                    return;
                }

                row.Progress += TD;
                row.Progress2 += ChaserKills;
                row.Progress3 += ChaserSelected;
                row.Progress4 += Kills;
                row.Progress5 += BRFirst;

                if (_owner.Room.Options.GameRule == GameRule.Touchdown)
                    row.Progress6++;

                row.Progress7 += KillsAssist;
                row.Progress8 += TDAssist;

                if (_owner.Room.Options.GameRule == GameRule.Deathmatch)
                    row.Progress9 += Kills;

                row.Progress10++;

                DbUtil.Update(connection, row);
            }
        }
    }

}

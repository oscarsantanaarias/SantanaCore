using Santana.Database.Game;
using Santana.Network.Message.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Santana
{
    internal class DailyMissionManager
    {
        readonly Player owner;

        public int TD;
        public int Kills;
        public int Progress;

        public DailyMissionManager(Player player)
        {
            owner = player;
        }

        public void DailyMiisonReset()
        {
            TD = 0;
            Kills = 0;
        }

        public void DailyMiison()
        {
            var date = DateTime.Now.ToString("dddd, dd MMMM yyyy");

            using (var conn = GameDatabase.Open())
            {
                var row = DbUtil.Find<Daily_MissionDto>(conn, statement => statement
                    .Where($"{nameof(Daily_MissionDto.PlayerId):C} = @{nameof(owner.Account.Id)} AND ({nameof(Daily_MissionDto.Date):C} = @{nameof(date)})")
                    .WithParameters(new { owner.Account.Id, date })).FirstOrDefault();

                if (row == null)
                    return;

                if (row.IsRewarded)
                    return;

                switch (row.Map)
                {
                    case 1:
                        if (owner.Room.Options.MapId == 66)
                            Progress = row.Progress += TD;
                        break;
                    case 2:
                        if (owner.Room.Options.MapId == 68)
                            Progress = row.Progress += TD;
                        break;
                    case 3:
                        if (owner.Room.Options.MapId == 231)
                            Progress = row.Progress += TD;
                        break;
                    case 4:
                        if (owner.Room.Options.MapId == 231)
                            Progress = row.Progress += Kills;
                        break;
                    case 5:
                        if (owner.Room.Options.MapId == 68)
                            Progress = row.Progress += Kills;
                        break;
                    case 6:
                        if (owner.Room.Options.MapId == 231)
                            Progress = row.Progress += Kills;
                        break;
                    case 7:
                        if (owner.Room.Options.MapId == 14)
                            Progress = row.Progress += Kills;
                        break;
                    case 8:
                        if (owner.Room.Options.MapId == 18)
                            Progress = row.Progress += Kills;
                        break;
                    case 9:
                        if (owner.Room.Options.MapId == 4)
                            Progress = row.Progress += Kills;
                        break;
                    case 10:
                        if (owner.Room.Options.GameRule == GameRule.Deathmatch)
                            Progress = row.Progress++;
                        break;
                    case 11:
                        if (owner.Room.Options.GameRule == GameRule.Touchdown)
                            Progress = row.Progress++;
                        break;
                    case 12:
                        if (owner.Room.Options.GameRule == GameRule.Chaser)
                            Progress = row.Progress++;
                        break;
                    case 13:
                        Progress = row.Progress++;
                        break;
                    default:
                        break;
                }

                DbUtil.Update(conn, row);

                owner?.SendAsync(new DailyMission_NoticeMessage { Unk = 1, GameMode = 0, Map = row.Map, MaxProgress = row.MaxProgress, Progress = Progress, Unk5 = 5, Unk6 = new int[] { row.Reward, row.Reward2, row.Reward3 } });
            }
        }
    }

}

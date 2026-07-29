
using ProudNetSrc.Serialization;
namespace Santana.Network.Data.GameRule
{
    [Dto]
    public class ArcadeScoreSyncDto
    {
         public ulong AccountId { get; set; }

         public int MonsterCount { get; set; }

         public int MaxMonster { get; set; }

         public int KilledMonster { get; set; }

    }

    [Dto]
    public class ArcadeScoreSyncReqDto
    {
         public ulong AccountId { get; set; }
         public int Unk1 { get; set; }
         public int Unk2 { get; set; }
         public int Unk3 { get; set; }
         public int Unk4 { get; set; }
         public int Unk5 { get; set; }
    }
}

using Santana.Resource.xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Santana.Resource
{
    public enum EsperSkillType
    {
        None = -1,
        Beam = 0,
        Coat = 1,
        Bomb = 2,
        MoneyRain = 3,
        KneeSlide = 4
    }

    public class EsperEnchant
    {
        public EsperEnchant()
        {
            Effects = Array.Empty<uint>();
        }

        public byte Level { get; set; }
        public EsperSkillType Type { get; set; }
        public ulong EsperId { get; set; }
        public int Rate { get; set; }
        public uint[] Effects { get; set; }
        public uint PEN { get; set; }

        public uint Effect => Effects.Length > 0 ? Effects[0] : 0;
    }
}

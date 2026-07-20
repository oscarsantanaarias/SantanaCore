using System;
using System.Collections.Generic;
using System.Linq;
namespace Santana.Resource
{
    public class EnchantSys
    {
        public byte Level { get; set; }
        public List<EnchantGroup> EnchantGroup { get; set; }

        public EnchantSys()
        {
        }

    }

    public class EnchantSystem
    {
        public uint Effect { get; set; }

        public byte Chance { get; set; }

        public EnchantSystem()
        {
        }
    }

    public class EnchantGroup
    {
        public Random Random;

        public ItemCategory Category { get; set; }
        public byte SubCategory { get; set; }

        public List<EnchantSystem> EnchantSystem { get; set; }

        public EnchantSystem Eff()
        {
            while (true)
            {
                foreach (var candidate in EnchantSystem)
                {
                    if (candidate.Chance >= Random.Next(101))
                        return candidate;
                }
            }
        }

        public EnchantGroup()
        {
            Random = new Random();
        }
    }

}

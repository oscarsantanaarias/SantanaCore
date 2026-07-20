using System;
using System.Collections.Generic;
using System.Text;

namespace Santana.Resource
{
    public class ItemEnchant
    {
        public int Id { get; set; }
        public byte Level { get; set; }
        public ItemCategory Category { get; set; }
        public byte SubCategory { get; set; }
        public int Chance { get; set; }
        public string Effects { get; set; }
    }
}

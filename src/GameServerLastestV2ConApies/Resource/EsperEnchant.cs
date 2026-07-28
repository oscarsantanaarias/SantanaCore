using Santana.Resource.xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Santana.Resource
{
    public class EsperEnchant
    {
        public byte Level { get; set; }
        public byte Type { get; set; }
        public ulong EsperId { get; set; }
        public int Rate { get; set; }
        public uint Effect { get; set; }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Santana.Resource.xml
{
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "EsperEnchant")]
    public class EsperSystemDto
    {
        [XmlElement("Esper")]
        public EsperEnchantSystemDto[] Espers { get; set; }
    }

    public class EsperEnchantSystemDto
    {
        [XmlAttribute("Level")]
        public byte Level { get; set; }

        [XmlAttribute("Type")]
        public byte Type { get; set; }

        [XmlAttribute("EsperId")]
        public ulong EsperId { get; set; }

        [XmlAttribute("Rate")]
        public int Rate { get; set; }

        [XmlElement("Effect")]
        public uint Effect { get; set; }
    }
}

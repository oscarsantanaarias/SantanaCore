using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Santana.Resource.xml
{
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "esper_enchant_info")]
    public class EsperSystemDto
    {
        [XmlAttribute("money_need")]
        public string MoneyNeed { get; set; }

        [XmlElement("esper_enchant_item_info")]
        public EsperEnchantSystemDto[] Espers { get; set; }
    }

    public class EsperEnchantSystemDto
    {
        [XmlAttribute("level")]
        public byte Level { get; set; }

        [XmlAttribute("type")]
        public string Type { get; set; }

        [XmlAttribute("shop_id")]
        public uint ShopId { get; set; }

        [XmlAttribute("prob")]
        public int Prob { get; set; }

        [XmlAttribute("effects")]
        public string Effects { get; set; }
    }
}

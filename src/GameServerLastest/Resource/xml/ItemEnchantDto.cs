using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace Santana.Resource.xml
{
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "Enchant")]
    public class ItemEnchantDto
    {
        [XmlElement("Enchant")]
        public ItemsEnchantDto[] Enchant { get; set; }
    }

    public class ItemsEnchantDto
    {
        [XmlAttribute("Id")]
        public int Id { get; set; }

        [XmlAttribute("Level")]
        public byte Level { get; set; }

        [XmlAttribute("Category")]
        public byte Category { get; set; }

        [XmlAttribute("Subcategory")]
        public byte SubCategory { get; set; }

        [XmlAttribute("Chance")]
        public int Chance { get; set; }

        [XmlAttribute("Effects")]
        public string Effects { get; set; }
    }
}

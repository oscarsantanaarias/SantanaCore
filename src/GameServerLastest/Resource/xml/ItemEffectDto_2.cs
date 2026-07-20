using System.Xml.Serialization;

namespace Santana.Resource.xml
{
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "S4_Effects")]
    public class ItemEffectDto_2
    {
        [XmlElement("Effect")] public ItemInfoEffectDto_2[] Effect { get; set; }
    }

    [XmlType(AnonymousType = true)]
    public class ItemInfoEffectDto_2
    {
        [XmlAttribute("ID")] public uint ID { get; set; }

        [XmlAttribute("Name")] public string Name { get; set; }
    }
}

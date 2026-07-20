using System.Runtime.CompilerServices;
using System.Xml.Serialization;

namespace Santana.Resource.xml
{
	[XmlRoot(Namespace = "", IsNullable = false, ElementName = "EnchantSystem")]
	[XmlType(AnonymousType = true)]
	public class EnchantSystemDto
	{
		[XmlElement("Level")]
		public LevelEnchantDto[] Levels { get; set; }
    }

    [XmlType(AnonymousType = true)]
    public class EnchantGroupDto
    {
        [XmlAttribute("category")] public ItemCategory Category { get; set; }
        [XmlAttribute("subcategory")] public byte SubCategory { get; set; }

        [XmlElement("Effect")] public EffectEnchantDto[] EffectEnchant { get; set; }

    }

    [XmlType(AnonymousType = true)]
    public class EffectEnchantDto
    {
        [XmlAttribute("value")] public uint Value { get; set; }

        [XmlAttribute("amount")] public int Amount { get; set; }
    }

    [XmlType(AnonymousType = true)]
    public class LevelEnchantDto
    {
        [XmlAttribute("value")] public byte Value { get; set; }

        [XmlElement("Group")] public EnchantGroupDto[] EnchantGroup { get; set; }

    }
}

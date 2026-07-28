using System.Xml.Serialization;

namespace Santana.Resource.xml
{
  [XmlType(AnonymousType = true)]
  [XmlRoot(Namespace = "", IsNullable = true, ElementName = "ItemReward")]
  public class ItemRewardDto
  {
    [XmlElement("item")] public ItemDto[] Items { get; set; }
  }

  [XmlType(AnonymousType = true)]
  public class ItemDto
  {
    [XmlAttribute] public uint Number { get; set; }

    [XmlElement("group")] public RewardGroup[] Groups { get; set; }
  }

  [XmlType(AnonymousType = true)]
  public class RewardGroup
  {
    [XmlElement("reward")] public RewardDto[] Rewards { get; set; }
  }

  [XmlType(AnonymousType = true)]
  public class RewardDto
  {
    [XmlAttribute] public uint Type { get; set; }

    [XmlAttribute] public uint Data { get; set; } = 0;

    [XmlAttribute] public uint PriceType { get; set; } = 0;

    [XmlAttribute] public uint PeriodType { get; set; } = 0;

    [XmlAttribute]
    public byte Color { get; set; } = 0;

    [XmlAttribute] public uint Value { get; set; }

    [XmlAttribute] public string Effects { get; set; } = "0";

    [XmlAttribute] public uint Rate { get; set; }
  }
}

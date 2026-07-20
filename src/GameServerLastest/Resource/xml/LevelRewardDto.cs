using System.Xml.Serialization;

namespace Santana.Resource.xml
{
    [XmlType(AnonymousType = true)]
    [XmlRoot(Namespace = "", IsNullable = false, ElementName = "LevelRewards")]
    public class LevelRewardDto
    {
        [XmlElement("Level")] public Level[] Levels { get; set; }
    }

    [XmlType(AnonymousType = true)]
    public class Level
    {
        [XmlAttribute] public int Number { get; set; }
        [XmlAttribute]  public uint Reward { get; set; }
        [XmlAttribute] public uint AP { get; set; }
        [XmlAttribute] public uint Pen { get; set; }

    }

}

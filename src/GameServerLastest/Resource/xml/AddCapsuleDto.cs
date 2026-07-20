using System.Collections.Generic;
using System.Xml.Serialization;

namespace Santana.Resource.xml
{
  [XmlRoot(ElementName = "capsule_icon")]
  public class Capsule_icon
  {
    [XmlAttribute(AttributeName = "ID_1")] public string ID_1 { get; set; }

    [XmlAttribute(AttributeName = "ID_2")] public string ID_2 { get; set; }

    [XmlAttribute(AttributeName = "ID_3")] public string ID_3 { get; set; }

    [XmlAttribute(AttributeName = "ID_4")] public string ID_4 { get; set; }

    [XmlAttribute(AttributeName = "ID_5")] public string ID_5 { get; set; }

    [XmlAttribute(AttributeName = "ID_6")] public string ID_6 { get; set; }

    [XmlAttribute(AttributeName = "ID_7")] public string ID_7 { get; set; }

    [XmlAttribute(AttributeName = "ID_8")] public string ID_8 { get; set; }

    [XmlAttribute(AttributeName = "ID_9")] public string ID_9 { get; set; }

    [XmlAttribute(AttributeName = "ID_10")]
    public string ID_10 { get; set; }

    [XmlAttribute(AttributeName = "ID_11")]
    public string ID_11 { get; set; }

    [XmlAttribute(AttributeName = "ID_16")]
    public string ID_16 { get; set; }

    [XmlAttribute(AttributeName = "ID_12")]
    public string ID_12 { get; set; }

    [XmlAttribute(AttributeName = "ID_14")]
    public string ID_14 { get; set; }

    [XmlAttribute(AttributeName = "ID_15")]
    public string ID_15 { get; set; }

    [XmlAttribute(AttributeName = "ID_13")]
    public string ID_13 { get; set; }
  }

  [XmlRoot(ElementName = "capsule_slot")]
  public class Capsule_slot
  {
    [XmlAttribute(AttributeName = "slot_1")]
    public string Slot_1 { get; set; }

    [XmlAttribute(AttributeName = "slot_2")]
    public string Slot_2 { get; set; }

    [XmlAttribute(AttributeName = "slot_3")]
    public string Slot_3 { get; set; }

    [XmlAttribute(AttributeName = "slot_4")]
    public string Slot_4 { get; set; }

    [XmlAttribute(AttributeName = "slot_5")]
    public string Slot_5 { get; set; }

    [XmlAttribute(AttributeName = "slot_6")]
    public string Slot_6 { get; set; }

    [XmlAttribute(AttributeName = "slot_7")]
    public string Slot_7 { get; set; }

    [XmlAttribute(AttributeName = "slot_8")]
    public string Slot_8 { get; set; }

    [XmlAttribute(AttributeName = "slot_9")]
    public string Slot_9 { get; set; }

    [XmlAttribute(AttributeName = "slot_10")]
    public string Slot_10 { get; set; }

    [XmlAttribute(AttributeName = "slot_11")]
    public string Slot_11 { get; set; }

    [XmlAttribute(AttributeName = "slot_15")]
    public string Slot_15 { get; set; }

    [XmlAttribute(AttributeName = "slot_16")]
    public string Slot_16 { get; set; }

    [XmlAttribute(AttributeName = "slot_14")]
    public string Slot_14 { get; set; }

    [XmlAttribute(AttributeName = "slot_12")]
    public string Slot_12 { get; set; }

    [XmlAttribute(AttributeName = "slot_13")]
    public string Slot_13 { get; set; }
  }

  [XmlRoot(ElementName = "capsule_info")]
  public class Capsule_info
  {
    [XmlAttribute(AttributeName = "effect_key_1")]
    public string Effect_key_1 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_2")]
    public string Effect_key_2 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_3")]
    public string Effect_key_3 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_4")]
    public string Effect_key_4 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_5")]
    public string Effect_key_5 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_6")]
    public string Effect_key_6 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_7")]
    public string Effect_key_7 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_8")]
    public string Effect_key_8 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_9")]
    public string Effect_key_9 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_10")]
    public string Effect_key_10 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_11")]
    public string Effect_key_11 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_14")]
    public string Effect_key_14 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_15")]
    public string Effect_key_15 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_16")]
    public string Effect_key_16 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_12")]
    public string Effect_key_12 { get; set; }

    [XmlAttribute(AttributeName = "effect_key_13")]
    public string Effect_key_13 { get; set; }
  }

  [XmlRoot(ElementName = "color_index")]
  public class Color_index
  {
    [XmlAttribute(AttributeName = "color_1")]
    public string Color_1 { get; set; }

    [XmlAttribute(AttributeName = "color_2")]
    public string Color_2 { get; set; }

    [XmlAttribute(AttributeName = "color_3")]
    public string Color_3 { get; set; }

    [XmlAttribute(AttributeName = "color_4")]
    public string Color_4 { get; set; }

    [XmlAttribute(AttributeName = "color_5")]
    public string Color_5 { get; set; }

    [XmlAttribute(AttributeName = "color_6")]
    public string Color_6 { get; set; }

    [XmlAttribute(AttributeName = "color_7")]
    public string Color_7 { get; set; }

    [XmlAttribute(AttributeName = "color_8")]
    public string Color_8 { get; set; }

    [XmlAttribute(AttributeName = "color_9")]
    public string Color_9 { get; set; }

    [XmlAttribute(AttributeName = "color_16")]
    public string Color_16 { get; set; }

    [XmlAttribute(AttributeName = "color_10")]
    public string Color_10 { get; set; }
  }

  [XmlRoot(ElementName = "item")]
  public class Item
  {
    [XmlElement(ElementName = "capsule_icon")]
    public Capsule_icon Capsule_icon { get; set; }

    [XmlElement(ElementName = "capsule_slot")]
    public Capsule_slot Capsule_slot { get; set; }

    [XmlElement(ElementName = "capsule_info")]
    public Capsule_info Capsule_info { get; set; }

    [XmlElement(ElementName = "color_index")]
    public Color_index Color_index { get; set; }

    [XmlAttribute(AttributeName = "id")] public string Id { get; set; }
  }

  [XmlRoot(ElementName = "Item_tooltip_addcapsule")]
  public class AddCapsuleDto
  {
    [XmlElement(ElementName = "item")] public List<Item> Item { get; set; }
  }
}

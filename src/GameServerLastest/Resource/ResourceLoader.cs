using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using SantanaLib.Configuration;
using Santana.Network;
using Santana.Resource.xml;
using Serilog;
using Serilog.Core;
namespace Santana.Resource
{
    internal class ResourceLoader
    {
        private static readonly ILogger _log =
            Log.ForContext(Constants.SourceContextPropertyName, nameof(ResourceLoader));
        public ResourceLoader(string resourcePath)
        {
            ResourcePath = resourcePath;
        }
        public string ResourcePath { get; }
        public byte[] GetBytes(string fileName)
        {
            var full = Path.Combine(ResourcePath, fileName.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(full))
                return null;
            return File.ReadAllBytes(full);
        }
        public IEnumerable<Experience> LoadExperience()
        {
            var expData = ReadXml<ExperienceDto>("xml/experience.x7");
            var level = 0;
            return expData.exp.Select(row => new Experience
            {
                Level = level++,
                ExperienceToNextLevel = row.require,
                TotalExperience = row.accumulate
            });
        }
        public IEnumerable<MapInfo> LoadMaps()
        {
            var strings = ReadXml<StringTableDto>("language/xml/gameinfo_string_table.x7");
            var mapData = ReadXml<MapInfoDto>("xml/map.x7");
            var byRule = new ConcurrentDictionary<Tuple<GameRule, byte>, MapInfo>();
            foreach (var entry in mapData.map)
            {
                var preview = entry.resource?.previewinfo_path ?? "";
                if (!preview.EndsWith(".tga") && !preview.EndsWith(".dds"))
                    continue;
                var euFlag = entry.Switch?.eu ?? "";
                var krFlag = entry.Switch?.kr ?? "";
                if (euFlag != "on" && krFlag != "on")
                    continue;
                var mapByte = unchecked((byte)entry.id);
                var info = new MapInfo
                {
                    Id = entry.id,
                    byteId = mapByte,
                    MinLevel = 0,
                    ServerId = 0,
                    ChannelId = 0,
                    RespawnType = 0,
                    MaxPlayers = entry.Base.limit_player,
                    IsRandom = entry.id > 900,
                    GameRule = (GameRule)entry.Base.mode_number
                };
                var label = new StringTableStringDto();
                try
                {
                    label = strings.@string.First(s =>
                        s.key.Equals(entry.Base.map_name_key, StringComparison.InvariantCultureIgnoreCase));
                }
                catch
                {
                    label.eng = "unknown";
                }
                if (string.IsNullOrWhiteSpace(label.eng))
                    label.eng = entry.Base.map_name_key;
                info.Name = label.eng;
                byRule.TryAdd(new Tuple<GameRule, byte>(info.GameRule, mapByte), info);
            }
            return byRule.Values;
        }
        public IEnumerable<ItemEffect> LoadEffects()
        {
            var effectData = ReadXml<ItemEffectDto>("xml/item_effect.x7");
            var strings = ReadXml<StringTableDto>("language/xml/item_effect_string_table.x7");
            var dumped = ReadXml<ItemEffectDto_2>("xml/dumpedeffects.xml");
            foreach (var row in effectData.item.Where(e => e.id != 0))
            {
                var built = new ItemEffect
                {
                    Id = row.id
                };
                foreach (var attr in row.attribute)
                {
                    built.Attributes.Add(new ItemEffectAttribute
                    {
                        Attribute = (Attribute)Enum.Parse(typeof(Attribute), attr.effect.Replace("_", ""), true),
                        Value = attr.value,
                        Rate = float.Parse(attr.rate, CultureInfo.InvariantCulture)
                    });
                }
                foreach (var info in dumped.Effect)
                {
                    built.EffectInfo.Add(new ItemEffectInfo
                    {
                        Id = info.ID,
                        Name = info.Name
                    });
                }
                var label = strings.@string.FirstOrDefault(s =>
                    s.key.Equals(row.text_key, StringComparison.InvariantCultureIgnoreCase));
                if (label == null)
                    label = new StringTableStringDto();
                if (string.IsNullOrWhiteSpace(label.eng))
                    label.eng = row.NAME;
                built.Name = label.eng;
                yield return built;
            }
        }
        public IEnumerable<GameTempo> LoadGameTempos()
        {
            var tempoData = ReadXml<ConstantInfoDto>("xml/constant_info.x7");
            foreach (var row in tempoData.GAMEINFOLIST)
            {
                var built = new GameTempo
                {
                    Name = row.TEMPVALUE.value
                };
                var common = row.GAMETEPMO_COMMON_TOTAL_VALUE;
                built.ActorDefaultHPMax =
                    float.Parse(common.GAMETEMPO_actor_default_hp_max, CultureInfo.InvariantCulture);
                built.ActorDefaultMPMax =
                    float.Parse(common.GAMETEMPO_actor_default_mp_max, CultureInfo.InvariantCulture);
                built.ActorDefaultMoveSpeed = common.GAMETEMPO_fastrun_required_mp;
                yield return built;
            }
        }
        public IEnumerable<DefaultItem> LoadDefaultItems()
        {
            var itemData = ReadXml<DefaultItemDto>("xml/default_item.x7");
            foreach (var row in itemData.male.item)
            {
                yield return new DefaultItem
                {
                    ItemNumber = new ItemNumber(row.category, row.sub_category, row.number),
                    Gender = CharacterGender.Male,
                    Variation = row.variation
                };
            }
            foreach (var row in itemData.female.item)
            {
                yield return new DefaultItem
                {
                    ItemNumber = new ItemNumber(row.category, row.sub_category, row.number),
                    Gender = CharacterGender.Female,
                    Variation = row.variation
                };
            }
        }
        private T ReadXml<T>(string fileName)
        {
            var xs = new XmlSerializer(typeof(T));
            var full = Path.Combine(ResourcePath, fileName.Replace('/', Path.DirectorySeparatorChar));
            using (var reader = new StreamReader(full))
            {
                return (T)xs.Deserialize(reader);
            }
        }
        public IEnumerable<ItemInfo> LoadItems()
        {
            var itemData = ReadXml<ItemInfoDto>("xml/iteminfo.x7");
            var strings = ReadXml<StringTableDto>("language/xml/iteminfo_string_table.xml");
            foreach (var cat in itemData.category)
            {
                foreach (var sub in cat.sub_category)
                {
                    foreach (var row in sub.item)
                    {
                        var number = new ItemNumber(cat.id, sub.id, row.number);
                        ItemInfo built;
                        switch (number.Category)
                        {
                            case ItemCategory.Skill:
                                built = BuildAction(number, row);
                                break;
                            case ItemCategory.Weapon:
                                built = BuildWeapon(number, row);
                                break;
                            default:
                                built = new ItemInfo();
                                break;
                        }
                        built.ItemNumber = number;
                        built.Level = row.@base.base_info.require_level;
                        built.MasterLevel = row.@base.base_info.require_master;
                        built.Gender = MapSex(row.SEX);
                        built.Image = row.client.icon.image;
                        if (row.@base.license != null)
                            built.License = MapLicense(row.@base.license.require);
                        var label = strings.@string.FirstOrDefault(s =>
                            s.key.Equals(row.@base.base_info.name_key,
                                StringComparison.InvariantCultureIgnoreCase));
                        if (string.IsNullOrWhiteSpace(label?.eng))
                            built.Name = label != null ? label.key : row.NAME;
                        else
                            built.Name = label.eng;
                        yield return built;
                    }
                }
            }
        }
        public IEnumerable<ItemInfo> LoadItems_2()
        {
            var itemData = ReadXml<ItemInfoDto_2>("xml/item.x7");
            var strings = ReadXml<StringTableDto_2>("language/xml/iteminfo_string_table.x7");
            var seen = new List<ItemNumber>();
            foreach (var row in itemData.item)
            {
                var number = new ItemNumber(row.item_key);
                if (seen.Contains(number))
                    continue;
                seen.Add(number);
                var built = new ItemInfo();
                built.ItemNumber = number;
                built.Level = 0;
                built.MasterLevel = 0;
                built.Gender = MapSex2(row.Base.sex);
                built.Image = row.graphic.icon_image;
                var label = strings.@string.FirstOrDefault(s =>
                    s.key.Equals(row.Base.name_key, StringComparison.InvariantCultureIgnoreCase));
                if (!string.IsNullOrWhiteSpace(label?.eng) && label?.eng.ToLower() != "no trans" &&
                    label?.eng.ToLower() != "not trans")
                    yield return built;
            }
        }
        public IEnumerable<ItemInfo> LoadItems_3()
        {
            var itemData = ReadXml<ItemInfoDto_2>("xml/item.x7");
            var dumped = ReadXml<ItemInfoDto_3>("xml/dumpeditems.xml");
            var strings = ReadXml<StringTableDto_2>("language/xml/iteminfo_string_table.x7");
            var byNumber = new Dictionary<ItemNumber, ItemInfo>();
            foreach (var row in itemData.item)
            {
                var number = new ItemNumber(row.item_key);
                if (!byNumber.Keys.Contains(number))
                {
                    var built = new ItemInfo();
                    built.ItemNumber = number;
                    built.Level = 0;
                    built.MasterLevel = 0;
                    built.Gender = MapSex2(row.Base.sex);
                    built.Image = row.graphic.icon_image;
                    byNumber.Add(number, built);
                }
            }
            foreach (var dumpRow in dumped.Item)
            {
                ItemInfo built;
                byNumber.TryGetValue(new ItemNumber(dumpRow.ID), out built);
                if (built != null)
                {
                    built.Colors = (int)dumpRow.Color_Count;
                    built.Name = dumpRow.Name;
                    if (!string.IsNullOrWhiteSpace(built.Name) &&
                        built.Name != "not trans" &&
                        built.Name != "no trans" &&
                        !string.IsNullOrWhiteSpace(built.Image))
                        yield return built;
                }
            }
        }
        public IEnumerable<ItemInfo> LoadItems_4()
        {
            var dumped = ReadXml<ItemInfoDto_3>("xml/dumpeditems.xml");
            var byName = new Dictionary<string, ItemInfo>();
            foreach (var row in dumped.Item)
            {
                if (!byName.Keys.Contains(row.Name))
                {
                    var built = new ItemInfo();
                    built.ItemNumber = row.ID;
                    byName.Add(row.Name, built);
                    if (!string.IsNullOrWhiteSpace(built.Name) &&
                        built.Name != "not trans" &&
                        built.Name != "no trans" &&
                        !string.IsNullOrWhiteSpace(built.Image))
                        yield return built;
                }
            }
        }
        private static ItemLicense MapLicense(string key)
        {
            Func<string, bool> matches = str => key.Equals(str, StringComparison.InvariantCultureIgnoreCase);
            if (matches("license_none"))
                return ItemLicense.None;
            if (matches("LICENSE_CHECK_NONE"))
                return ItemLicense.None;
            if (matches("LICENSE_PLASMA_SWORD"))
                return ItemLicense.PlasmaSword;
            if (matches("license_counter_sword"))
                return ItemLicense.CounterSword;
            if (matches("LICENSE_STORM_BAT"))
                return ItemLicense.StormBat;
            if (matches("LICENSE_ASSASSIN_CLAW"))
                return ItemLicense.None;
            if (matches("LICENSE_SUBMACHINE_GUN"))
                return ItemLicense.SubmachineGun;
            if (matches("license_revolver"))
                return ItemLicense.Revolver;
            if (matches("license_semi_rifle"))
                return ItemLicense.SemiRifle;
            if (matches("LICENSE_SMG3"))
                return ItemLicense.None;
            if (matches("license_HAND_GUN"))
                return ItemLicense.None;
            if (matches("LICENSE_SMG4"))
                return ItemLicense.None;
            if (matches("LICENSE_HEAVYMACHINE_GUN"))
                return ItemLicense.HeavymachineGun;
            if (matches("LICENSE_GAUSS_RIFLE"))
                return ItemLicense.GaussRifle;
            if (matches("license_rail_gun"))
                return ItemLicense.RailGun;
            if (matches("license_cannonade"))
                return ItemLicense.Cannonade;
            if (matches("LICENSE_CENTRYGUN"))
                return ItemLicense.Sentrygun;
            if (matches("license_centi_force"))
                return ItemLicense.SentiForce;
            if (matches("LICENSE_SENTINEL"))
                return ItemLicense.SentiNel;
            if (matches("license_mine_gun"))
                return ItemLicense.MineGun;
            if (matches("LICENSE_MIND_ENERGY"))
                return ItemLicense.MindEnergy;
            if (matches("license_mind_shock"))
                return ItemLicense.MindShock;
            if (matches("LICENSE_ANCHORING"))
                return ItemLicense.Anchoring;
            if (matches("LICENSE_FLYING"))
                return ItemLicense.Flying;
            if (matches("LICENSE_INVISIBLE"))
                return ItemLicense.Invisible;
            if (matches("license_detect"))
                return ItemLicense.Detect;
            if (matches("LICENSE_SHIELD"))
                return ItemLicense.Shield;
            if (matches("LICENSE_BLOCK"))
                return ItemLicense.Block;
            if (matches("LICENSE_BIND"))
                return ItemLicense.Bind;
            if (matches("LICENSE_METALLIC"))
                return ItemLicense.Metallic;
            throw new Exception("Invalid license " + key);
        }
        private static Gender MapSex(string sex)
        {
            Func<string, bool> matches = str => sex.Equals(str, StringComparison.InvariantCultureIgnoreCase);
            if (matches("all"))
                return Gender.None;
            if (matches("woman"))
                return Gender.Female;
            if (matches("man"))
                return Gender.Male;
            return Gender.None;
        }
        private static Gender MapSex2(string sex)
        {
            if (sex == "man")
                return Gender.Male;
            if (sex == "woman")
                return Gender.Female;
            if (sex == "unisex")
                return Gender.None;
            return Gender.None;
        }
        private static ItemInfo BuildAction(ItemNumber id, ItemInfoItemDto row)
        {
            if (row.action == null)
            {
                return new ItemInfoAction();
            }
            var built = new ItemInfoAction
            {
                RequiredMP = float.Parse(row.action.ability.required_mp, CultureInfo.InvariantCulture),
                DecrementMP = float.Parse(row.action.ability.decrement_mp, CultureInfo.InvariantCulture),
                DecrementMPDelay = float.Parse(row.action.ability.decrement_mp_delay, CultureInfo.InvariantCulture)
            };
            if (row.action.@float != null)
                built.ValuesF = row.action.@float
                    .Select(f => float.Parse(f.value.Replace("f", ""), CultureInfo.InvariantCulture)).ToList();
            if (row.action.integer != null)
                built.Values = row.action.integer.Select(i => i.value).ToList();
            return built;
        }
        private static ItemInfo BuildWeapon(ItemNumber id, ItemInfoItemDto row)
        {
            if (row.weapon == null)
                return new ItemInfoWeapon();
            var ab = row.weapon.ability;
            var built = new ItemInfoWeapon
            {
                Type = ab.type,
                RateOfFire = float.Parse(ab.rate_of_fire, CultureInfo.InvariantCulture),
                Power = float.Parse(ab.power, CultureInfo.InvariantCulture),
                MoveSpeedRate = float.Parse(ab.move_speed_rate, CultureInfo.InvariantCulture),
                AttackMoveSpeedRate = float.Parse(ab.attack_move_speed_rate, CultureInfo.InvariantCulture),
                MagazineCapacity = ab.magazine_capacity,
                CrackedMagazineCapacity = ab.cracked_magazine_capacity,
                MaxAmmo = ab.max_ammo,
                Accuracy = float.Parse(ab.accuracy, CultureInfo.InvariantCulture),
                Range = string.IsNullOrWhiteSpace(ab.range)
                    ? 0
                    : float.Parse(ab.range, CultureInfo.InvariantCulture),
                SupportSniperMode = ab.support_sniper_mode > 0,
                SniperModeFov = ab.sniper_mode_fov > 0,
                AutoTargetDistance = ab.auto_target_distance == null
                    ? 0
                    : float.Parse(ab.auto_target_distance, CultureInfo.InvariantCulture)
            };
            if (row.weapon.@float != null)
                built.ValuesF = row.weapon.@float
                    .Select(f => float.Parse(f.value.Replace("f", ""), CultureInfo.InvariantCulture)).ToList();
            if (row.weapon.integer != null)
                built.Values = row.weapon.integer.Select(i => i.value).ToList();
            return built;
        }
        public IEnumerable<ItemNumber> GetWorkingCapsules()
        {
            var capsuleData = ReadXml<AddCapsuleDto>("xml/_eu_item_tooltip_addcapsule.x7");
            var working = new List<ItemNumber>();
            foreach (var row in capsuleData.Item)
            {
                var foundItem = false;
                var iconSet = row.Capsule_icon;
                var colorSet = row.Color_index;
                var effectSet = row.Capsule_info;
                var slotSet = row.Capsule_slot;
                var itemSlots = new ConcurrentDictionary<ItemNumber, int>();
                var slotRewards = new ConcurrentDictionary<int, List<CapsuleReward>>();
                var slotStack = new ConcurrentStack<int>();
                var slotColors = new ConcurrentDictionary<int, int>();
                slotStack.Push(0);
                if (int.TryParse(slotSet.Slot_1, out var sl1))
                {
                    slotRewards.TryAdd(sl1, new List<CapsuleReward>());
                    slotStack.Push(sl1);
                }
                if (int.TryParse(slotSet.Slot_2, out var sl2))
                {
                    slotRewards.TryAdd(sl2, new List<CapsuleReward>());
                    slotStack.Push(sl2);
                }
                if (int.TryParse(slotSet.Slot_3, out var sl3))
                {
                    slotRewards.TryAdd(sl3, new List<CapsuleReward>());
                    slotStack.Push(sl3);
                }
                if (int.TryParse(slotSet.Slot_4, out var sl4))
                {
                    slotRewards.TryAdd(sl4, new List<CapsuleReward>());
                    slotStack.Push(sl4);
                }
                if (int.TryParse(slotSet.Slot_5, out var sl5))
                {
                    slotRewards.TryAdd(sl5, new List<CapsuleReward>());
                    slotStack.Push(sl5);
                }
                if (int.TryParse(slotSet.Slot_6, out var sl6))
                {
                    slotRewards.TryAdd(sl6, new List<CapsuleReward>());
                    slotStack.Push(sl6);
                }
                if (int.TryParse(slotSet.Slot_7, out var sl7))
                {
                    slotRewards.TryAdd(sl7, new List<CapsuleReward>());
                    slotStack.Push(sl7);
                }
                if (int.TryParse(slotSet.Slot_8, out var sl8))
                {
                    slotRewards.TryAdd(sl8, new List<CapsuleReward>());
                    slotStack.Push(sl8);
                }
                if (int.TryParse(slotSet.Slot_9, out var sl9))
                {
                    slotRewards.TryAdd(sl9, new List<CapsuleReward>());
                    slotStack.Push(sl9);
                }
                if (int.TryParse(slotSet.Slot_10, out var sl10))
                {
                    slotRewards.TryAdd(sl10, new List<CapsuleReward>());
                    slotStack.Push(sl10);
                }
                if (int.TryParse(slotSet.Slot_11, out var sl11))
                {
                    slotRewards.TryAdd(sl11, new List<CapsuleReward>());
                    slotStack.Push(sl11);
                }
                if (int.TryParse(slotSet.Slot_15, out var sl15))
                {
                    slotRewards.TryAdd(sl15, new List<CapsuleReward>());
                    slotStack.Push(sl15);
                }
                if (int.TryParse(slotSet.Slot_16, out var sl16))
                {
                    slotRewards.TryAdd(sl16, new List<CapsuleReward>());
                    slotStack.Push(sl16);
                }
                if (int.TryParse(slotSet.Slot_14, out var sl14))
                {
                    slotRewards.TryAdd(sl14, new List<CapsuleReward>());
                    slotStack.Push(sl14);
                }
                if (int.TryParse(slotSet.Slot_12, out var sl12))
                {
                    slotRewards.TryAdd(sl12, new List<CapsuleReward>());
                    slotStack.Push(sl12);
                }
                if (int.TryParse(slotSet.Slot_13, out var sl13))
                {
                    slotRewards.TryAdd(sl13, new List<CapsuleReward>());
                    slotStack.Push(sl13);
                }
                if (slotRewards.TryGetValue(sl1, out var bucket1) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_1), out CapsuleReward rw1))
                    bucket1.Add(rw1);
                if (slotRewards.TryGetValue(sl2, out var bucket2) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_2), out CapsuleReward rw2))
                    bucket2.Add(rw2);
                if (slotRewards.TryGetValue(sl3, out var bucket3) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_3), out CapsuleReward rw3))
                    bucket3.Add(rw3);
                if (slotRewards.TryGetValue(sl4, out var bucket4) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_4), out CapsuleReward rw4))
                    bucket4.Add(rw4);
                if (slotRewards.TryGetValue(sl5, out var bucket5) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_5), out CapsuleReward rw5))
                    bucket5.Add(rw5);
                if (slotRewards.TryGetValue(sl6, out var bucket6) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_6), out CapsuleReward rw6))
                    bucket6.Add(rw6);
                if (slotRewards.TryGetValue(sl7, out var bucket7) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_7), out CapsuleReward rw7))
                    bucket7.Add(rw7);
                if (slotRewards.TryGetValue(sl8, out var bucket8) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_8), out CapsuleReward rw8))
                    bucket8.Add(rw8);
                if (slotRewards.TryGetValue(sl9, out var bucket9) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_9), out CapsuleReward rw9))
                    bucket9.Add(rw9);
                if (slotRewards.TryGetValue(sl10, out var bucket10) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_10), out CapsuleReward rw10))
                    bucket10.Add(rw10);
                if (slotRewards.TryGetValue(sl11, out var bucket11) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_11), out CapsuleReward rw11))
                    bucket11.Add(rw11);
                if (slotRewards.TryGetValue(sl14, out var bucket14) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_14), out CapsuleReward rw14))
                    bucket14.Add(rw14);
                if (slotRewards.TryGetValue(sl15, out var bucket15) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_15), out CapsuleReward rw15))
                    bucket15.Add(rw15);
                if (slotRewards.TryGetValue(sl16, out var bucket16) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_16), out CapsuleReward rw16))
                    bucket16.Add(rw16);
                if (slotRewards.TryGetValue(sl12, out var bucket12) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_12), out CapsuleReward rw12))
                    bucket12.Add(rw12);
                if (slotRewards.TryGetValue(sl13, out var bucket13) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_13), out CapsuleReward rw13))
                    bucket13.Add(rw13);
                if (int.TryParse(iconSet.ID_1, out var pid1))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_2, out var pid2))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_3, out var pid3))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_4, out var pid4))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_5, out var pid5))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_6, out var pid6))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_7, out var pid7))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_8, out var pid8))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_9, out var pid9))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_10, out var pid10))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_11, out var pid11))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_15, out var pid15))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_16, out var pid16))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_14, out var pid14))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_12, out var pid12))
                    foundItem = true;
                if (int.TryParse(iconSet.ID_13, out var pid13))
                    foundItem = true;
                if (foundItem)
                    working.Add(int.Parse(row.Id));
            }
            return working;
        }
        public IEnumerable<AddCapsule> LoadCapsules()
        {
            var capsuleData = ReadXml<AddCapsuleDto>("xml/_eu_item_tooltip_addcapsule.x7");
            var caps = new ConcurrentDictionary<ItemNumber, AddCapsule>();
            foreach (var row in capsuleData.Item)
            {
                var built = new AddCapsule(int.Parse(row.Id));
                var iconSet = row.Capsule_icon;
                var colorSet = row.Color_index;
                var effectSet = row.Capsule_info;
                var slotSet = row.Capsule_slot;
                var itemSlots = new ConcurrentDictionary<ItemNumber, int>();
                var slotRewards = new ConcurrentDictionary<int, List<CapsuleReward>>();
                var slotStack = new ConcurrentStack<int>();
                var slotColors = new ConcurrentDictionary<int, int>();
                var usedNames = new ConcurrentDictionary<int, List<string>>();
                slotStack.Push(0);
                slotRewards.TryAdd(0, new List<CapsuleReward>());
                if (int.TryParse(slotSet.Slot_1, out var sl1))
                {
                    slotRewards.TryAdd(sl1, new List<CapsuleReward>());
                    slotStack.Push(sl1);
                }
                if (int.TryParse(slotSet.Slot_2, out var sl2))
                {
                    slotRewards.TryAdd(sl2, new List<CapsuleReward>());
                    slotStack.Push(sl2);
                }
                if (int.TryParse(slotSet.Slot_3, out var sl3))
                {
                    slotRewards.TryAdd(sl3, new List<CapsuleReward>());
                    slotStack.Push(sl3);
                }
                if (int.TryParse(slotSet.Slot_4, out var sl4))
                {
                    slotRewards.TryAdd(sl4, new List<CapsuleReward>());
                    slotStack.Push(sl4);
                }
                if (int.TryParse(slotSet.Slot_5, out var sl5))
                {
                    slotRewards.TryAdd(sl5, new List<CapsuleReward>());
                    slotStack.Push(sl5);
                }
                if (int.TryParse(slotSet.Slot_6, out var sl6))
                {
                    slotRewards.TryAdd(sl6, new List<CapsuleReward>());
                    slotStack.Push(sl6);
                }
                if (int.TryParse(slotSet.Slot_7, out var sl7))
                {
                    slotRewards.TryAdd(sl7, new List<CapsuleReward>());
                    slotStack.Push(sl7);
                }
                if (int.TryParse(slotSet.Slot_8, out var sl8))
                {
                    slotRewards.TryAdd(sl8, new List<CapsuleReward>());
                    slotStack.Push(sl8);
                }
                if (int.TryParse(slotSet.Slot_9, out var sl9))
                {
                    slotRewards.TryAdd(sl9, new List<CapsuleReward>());
                    slotStack.Push(sl9);
                }
                if (int.TryParse(slotSet.Slot_10, out var sl10))
                {
                    slotRewards.TryAdd(sl10, new List<CapsuleReward>());
                    slotStack.Push(sl10);
                }
                if (int.TryParse(slotSet.Slot_11, out var sl11))
                {
                    slotRewards.TryAdd(sl11, new List<CapsuleReward>());
                    slotStack.Push(sl11);
                }
                if (int.TryParse(slotSet.Slot_15, out var sl15))
                {
                    slotRewards.TryAdd(sl15, new List<CapsuleReward>());
                    slotStack.Push(sl15);
                }
                if (int.TryParse(slotSet.Slot_16, out var sl16))
                {
                    slotRewards.TryAdd(sl16, new List<CapsuleReward>());
                    slotStack.Push(sl16);
                }
                if (int.TryParse(slotSet.Slot_14, out var sl14))
                {
                    slotRewards.TryAdd(sl14, new List<CapsuleReward>());
                    slotStack.Push(sl14);
                }
                if (int.TryParse(slotSet.Slot_12, out var sl12))
                {
                    slotRewards.TryAdd(sl12, new List<CapsuleReward>());
                    slotStack.Push(sl12);
                }
                if (int.TryParse(slotSet.Slot_13, out var sl13))
                {
                    slotRewards.TryAdd(sl13, new List<CapsuleReward>());
                    slotStack.Push(sl13);
                }
                if (int.TryParse(colorSet.Color_1, out var clr1))
                    slotColors.TryAdd(sl1, clr1);
                if (int.TryParse(colorSet.Color_2, out var clr2))
                    slotColors.TryAdd(sl2, clr2);
                if (int.TryParse(colorSet.Color_3, out var clr3))
                    slotColors.TryAdd(sl3, clr3);
                if (int.TryParse(colorSet.Color_4, out var clr4))
                    slotColors.TryAdd(sl4, clr4);
                if (int.TryParse(colorSet.Color_5, out var clr5))
                    slotColors.TryAdd(sl5, clr5);
                if (int.TryParse(colorSet.Color_6, out var clr6))
                    slotColors.TryAdd(sl6, clr6);
                if (int.TryParse(colorSet.Color_7, out var clr7))
                    slotColors.TryAdd(sl7, clr7);
                if (int.TryParse(colorSet.Color_8, out var clr8))
                    slotColors.TryAdd(sl8, clr8);
                if (int.TryParse(colorSet.Color_9, out var clr9))
                    slotColors.TryAdd(sl9, clr9);
                if (int.TryParse(colorSet.Color_10, out var clr10))
                    slotColors.TryAdd(sl10, clr10);
                if (int.TryParse(colorSet.Color_16, out var clr16))
                    slotColors.TryAdd(sl16, clr16);
                if (slotRewards.TryGetValue(sl1, out var bucket1) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_1), out CapsuleReward rw1))
                    bucket1.Add(rw1);
                if (slotRewards.TryGetValue(sl2, out var bucket2) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_2), out CapsuleReward rw2))
                    bucket2.Add(rw2);
                if (slotRewards.TryGetValue(sl3, out var bucket3) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_3), out CapsuleReward rw3))
                    bucket3.Add(rw3);
                if (slotRewards.TryGetValue(sl4, out var bucket4) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_4), out CapsuleReward rw4))
                    bucket4.Add(rw4);
                if (slotRewards.TryGetValue(sl5, out var bucket5) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_5), out CapsuleReward rw5))
                    bucket5.Add(rw5);
                if (slotRewards.TryGetValue(sl6, out var bucket6) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_6), out CapsuleReward rw6))
                    bucket6.Add(rw6);
                if (slotRewards.TryGetValue(sl7, out var bucket7) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_7), out CapsuleReward rw7))
                    bucket7.Add(rw7);
                if (slotRewards.TryGetValue(sl8, out var bucket8) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_8), out CapsuleReward rw8))
                    bucket8.Add(rw8);
                if (slotRewards.TryGetValue(sl9, out var bucket9) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_9), out CapsuleReward rw9))
                    bucket9.Add(rw9);
                if (slotRewards.TryGetValue(sl10, out var bucket10) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_10), out CapsuleReward rw10))
                    bucket10.Add(rw10);
                if (slotRewards.TryGetValue(sl11, out var bucket11) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_11), out CapsuleReward rw11))
                    bucket11.Add(rw11);
                if (slotRewards.TryGetValue(sl14, out var bucket14) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_14), out CapsuleReward rw14))
                    bucket14.Add(rw14);
                if (slotRewards.TryGetValue(sl15, out var bucket15) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_15), out CapsuleReward rw15))
                    bucket15.Add(rw15);
                if (slotRewards.TryGetValue(sl16, out var bucket16) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_16), out CapsuleReward rw16))
                    bucket16.Add(rw16);
                if (slotRewards.TryGetValue(sl12, out var bucket12) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_12), out CapsuleReward rw12))
                    bucket12.Add(rw12);
                if (slotRewards.TryGetValue(sl13, out var bucket13) &&
                    Enum.TryParse(AddCapsule.ConvertCapsuleReward(effectSet.Effect_key_13), out CapsuleReward rw13))
                    bucket13.Add(rw13);
                if (int.TryParse(iconSet.ID_1, out var pid1))
                    itemSlots.TryAdd(pid1, sl1);
                if (int.TryParse(iconSet.ID_2, out var pid2))
                    itemSlots.TryAdd(pid2, sl2);
                if (int.TryParse(iconSet.ID_3, out var pid3))
                    itemSlots.TryAdd(pid3, sl3);
                if (int.TryParse(iconSet.ID_4, out var pid4))
                    itemSlots.TryAdd(pid4, sl4);
                if (int.TryParse(iconSet.ID_5, out var pid5))
                    itemSlots.TryAdd(pid5, sl5);
                if (int.TryParse(iconSet.ID_6, out var pid6))
                    itemSlots.TryAdd(pid6, sl6);
                if (int.TryParse(iconSet.ID_7, out var pid7))
                    itemSlots.TryAdd(pid7, sl7);
                if (int.TryParse(iconSet.ID_8, out var pid8))
                    itemSlots.TryAdd(pid8, sl8);
                if (int.TryParse(iconSet.ID_9, out var pid9))
                    itemSlots.TryAdd(pid9, sl9);
                if (int.TryParse(iconSet.ID_10, out var pid10))
                    itemSlots.TryAdd(pid10, sl10);
                if (int.TryParse(iconSet.ID_11, out var pid11))
                    itemSlots.TryAdd(pid11, sl11);
                if (int.TryParse(iconSet.ID_15, out var pid15))
                    itemSlots.TryAdd(pid15, sl15);
                if (int.TryParse(iconSet.ID_16, out var pid16))
                    itemSlots.TryAdd(pid16, sl16);
                if (int.TryParse(iconSet.ID_14, out var pid14))
                    itemSlots.TryAdd(pid14, sl14);
                if (int.TryParse(iconSet.ID_12, out var pid12))
                    itemSlots.TryAdd(pid12, sl12);
                if (int.TryParse(iconSet.ID_13, out var pid13))
                    itemSlots.TryAdd(pid13, sl13);
                var prizeList = new List<AddCapsuleReward>();
                foreach (var slotId in slotStack)
                    prizeList.Add(new AddCapsuleReward(slotId));
                var shopRes = GameServer.Instance.ResourceCache.GetShop();
                var allItems = GameServer.Instance.ResourceCache.GetItems();
                foreach (var entry in itemSlots)
                {
                    if (slotStack.Contains(entry.Value))
                    {
                        var matched = allItems.FirstOrDefault(x => x.Key == entry.Key);
                        if (matched.Value == null)
                            continue;
                        if (matched.Value?.Name.Contains("(7/15/30") ?? false)
                        {
                            var alt = allItems.FirstOrDefault(x =>
                                x.Value.Name.Trim().Contains(
                                    matched.Value?.Name
                                        .Replace("(7/15/30/Permanent)", "(Permanent)")
                                        .Replace("(7/15/30)", "(Permanent)")
                                        .Replace("(7/15/30 Days/perm)", "(perm)")
                                        .Trim()));
                            if (alt.Value == null)
                            {
                                alt = allItems.FirstOrDefault(x =>
                                    x.Value.Name.Trim().Contains(
                                        matched.Value?.Name
                                            .Replace("(7/15/30/Permanent)", " (Permanent)")
                                            .Replace("(7/15/30)", " (Permanent)")
                                            .Replace("(7/15/30 Days/perm)", " (perm)")
                                            .Trim()));
                            }
                            if (alt.Value == null)
                            {
                                alt = allItems.FirstOrDefault(x =>
                                    x.Value.Name.Trim().Equals(
                                        matched.Value?.Name
                                            .Replace("(7/15/30/Permanent)", string.Empty)
                                            .Replace("(7/15/30)", string.Empty)
                                            .Replace("(7/15/30 Days/perm)", string.Empty)
                                            .Trim()));
                            }
                            if (alt.Value == null)
                            {
                                alt = allItems.FirstOrDefault(x =>
                                    x.Value.Name.Trim().Contains(
                                        matched.Value?.Name
                                            .Replace("(AP)", " ")
                                            .Replace("(7/15/30/Permanent)", "(Permanent)")
                                            .Replace("(7/15/30)", "(Permanent)")
                                            .Replace("(7/15/30 Days/perm)", "(perm)")
                                            .Trim()));
                            }
                            if (alt.Value == null)
                            {
                                alt = allItems.FirstOrDefault(x =>
                                    x.Value.Name.Trim().Contains(
                                        matched.Value?.Name
                                            .Replace("(AP)", " ")
                                            .Replace("(7/15/30/Permanent)", " (Permanent)")
                                            .Replace("(7/15/30)", " (Permanent)")
                                            .Replace("(7/15/30 Days/perm)", " (perm)")
                                            .Trim()));
                            }
                            if (alt.Value == null)
                            {
                                alt = allItems.FirstOrDefault(x =>
                                    x.Value.Name.Trim().Equals(
                                        matched.Value?.Name
                                            .Replace("(AP)", " ")
                                            .Replace("(7/15/30/Permanent)", string.Empty)
                                            .Replace("(7/15/30)", string.Empty)
                                            .Replace("(7/15/30 Days/perm)", string.Empty)
                                            .Trim()));
                            }
                            if (alt.Value != null)
                                itemSlots.TryAdd(alt.Key, entry.Value);
                            continue;
                        }
                        if (shopRes.Items.TryGetValue(entry.Key, out var shopEntry))
                        {
                            var prizeRow = prizeList.FirstOrDefault(x => x.SlotId == entry.Value);
                            if (prizeRow != null)
                            {
                                slotColors.TryGetValue(entry.Value, out var colorIndex);
                                var hasColor =
                                    shopRes.Items.Any(x => x.Key == entry.Key && x.Value.ColorGroup > colorIndex);
                                if (!hasColor)
                                    colorIndex = 0;
                                var already = false;
                                if (!usedNames.ContainsKey(prizeRow.SlotId))
                                    usedNames.TryAdd(prizeRow.SlotId, new List<string>());
                                usedNames.TryGetValue(prizeRow.SlotId, out var nameBucket);
                                var tokenName = matched.Value?.Name
                                    .Replace("(1 Day)", $"{prizeRow.SlotId}-T")
                                    .Replace("(7 Days)", $"{prizeRow.SlotId}-T")
                                    .Replace("(15 Days)", $"{prizeRow.SlotId}-T")
                                    .Replace("(30 Days)", $"{prizeRow.SlotId}-T")
                                    .Replace("(Permanent)", $"{prizeRow.SlotId}-T")
                                    .Trim() ?? "";
                                if (nameBucket.Contains(tokenName))
                                    already = true;
                                else
                                    nameBucket.Add(tokenName);
                                if (!already)
                                {
                                    prizeRow.Items.Add(shopEntry, colorIndex);
                                    built.Ready = true;
                                }
                            }
                        }
                    }
                }
                foreach (var rewardEntry in slotRewards)
                {
                    if (slotStack.Contains(rewardEntry.Key))
                    {
                        var prizeRow = prizeList.FirstOrDefault(x => x.SlotId == rewardEntry.Key);
                        prizeRow.Rewards.AddRange(rewardEntry.Value);
                    }
                }
                foreach (var prizeItem in prizeList)
                {
                    if (prizeItem.Items.Any() || prizeItem.Rewards.Any())
                        built.Prizes.TryAdd(prizeItem.SlotId, prizeItem);
                }
                if (built.Prizes.Any())
                    caps.TryAdd(built.CapsuleItemId, built);
            }
            return caps.Values;
        }
        public IEnumerable<CapsuleRewards> LoadItemRewards()
        {
            var bagData = ReadXml<ItemRewardDto>("xml/ItemBag.xml");
            if (bagData?.Items != null)
                foreach (var itemRow in bagData.Items)
                {
                    var rewardSet = new CapsuleRewards { Item = itemRow.Number, Bags = new List<BagReward>() };
                    if (itemRow.Groups != null)
                    {
                        foreach (var groupRow in itemRow.Groups)
                        {
                            var bagRow = new BagReward
                            {
                                Bag = new List<ItemReward>()
                            };
                            if (groupRow.Rewards != null)
                            {
                                foreach (var rewardRow in groupRow.Rewards)
                                {
                                    var penValue = (CapsuleRewardType)rewardRow.Type == CapsuleRewardType.PEN
                                        ? rewardRow.Value
                                        : 0;
                                    var periodValue = (CapsuleRewardType)rewardRow.Type == CapsuleRewardType.PEN
                                        ? 0
                                        : rewardRow.Value;
                                    bagRow.Bag.Add(new ItemReward
                                    {
                                        Type = (CapsuleRewardType)rewardRow.Type,
                                        Item = rewardRow.Data,
                                        PriceType = (ItemPriceType)rewardRow.PriceType,
                                        PeriodType = (ItemPeriodType)rewardRow.PeriodType,
                                        Period = periodValue,
                                        PEN = penValue,
                                        Effects = rewardRow.Effects.Split(",").Select(e => uint.Parse(e)).ToArray(),
                                        Rate = rewardRow.Rate,
                                        Color = rewardRow.Color,
                                        Value = rewardRow.Value
                                    });
                                }
                            }
                            rewardSet.Bags.Add(bagRow);
                        }
                    }
                    yield return rewardSet;
                }
        }
        public IEnumerable<ItemEnchant> LoadItemEnchant()
        {
            var enchantData = ReadXml<ItemEnchantDto>("xml/Enchant.xml");
            var enchantRows = enchantData.Enchant;
            foreach (var row in enchantRows)
            {
                yield return new ItemEnchant
                {
                    Id = row.Id,
                    Level = row.Level,
                    Category = (ItemCategory)row.Category,
                    SubCategory = row.SubCategory,
                    Chance = row.Chance,
                    Effects = row.Effects
                };
            }
        }
        public IEnumerable<EsperEnchant> LoadEsperEnchant()
        {
            var esperData = ReadXml<EsperSystemDto>("xml/Esper.xml");
            var esperRows = esperData.Espers;
            foreach (var row in esperRows)
            {
                yield return new EsperEnchant
                {
                    Level = row.Level,
                    EsperId = row.EsperId,
                    Rate = row.Rate,
                    Effect = row.Effect
                };
            }
        }
    }
}

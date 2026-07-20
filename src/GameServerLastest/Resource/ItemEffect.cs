using System.Collections.Generic;

namespace Santana.Resource
{
    public class ItemEffect
    {
        public ItemEffect()
        {
            Attributes = new List<ItemEffectAttribute>();
            EffectInfo = new List<ItemEffectInfo>();
        }

        public uint Id { get; set; }
        public string Name { get; set; }
        public IList<ItemEffectAttribute> Attributes { get; set; }
        public IList<ItemEffectInfo> EffectInfo { get; set; }
        public override string ToString()
        {
            return $"{Id}-{Name}";
        }
    }

    public class ItemEffectAttribute
    {
        public Attribute Attribute { get; set; }
        public int Value { get; set; }
        public float Rate { get; set; }
    }

    public class ItemEffectInfo
    {
        public uint Id { get; set; }
        public string Name { get; set; }
    }
}

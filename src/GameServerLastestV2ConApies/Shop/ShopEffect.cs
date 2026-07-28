using System.Collections.Generic;
using Santana.Database.Game;

namespace Santana.Shop
{
    internal class ShopEffectGroup
    {
        public ShopEffectGroup(ShopEffectGroupDto dto)
        {
            Id = dto.Id;
            Name = dto.Name;
            MainEffect = dto.Effect;

            var collected = new List<ShopEffect>();
            foreach (var effectDto in dto.ShopEffects)
                collected.Add(new ShopEffect(effectDto));
            Effects = collected;
        }

        public int Id { get; set; }
        public string Name { get; set; }
        public IList<ShopEffect> Effects { get; set; }
        public uint MainEffect { get; set; }

        public ShopEffect GetEffect(int id)
        {
            foreach (var effect in Effects)
                if (effect.Id == id)
                    return effect;

            return null;
        }
    }

    internal class ShopEffect
    {
        public ShopEffect(ShopEffectDto dto)
        {
            Id = dto.Id;
            Effect = dto.Effect;
        }

        public int Id { get; set; }
        public uint Effect { get; set; }
    }
}

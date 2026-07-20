using System;
using System.Collections.Generic;
using System.Linq;

namespace Santana.Resource
{
  internal class CapsuleRewards
  {
    public ItemNumber Item { get; set; }

    public List<BagReward> Bags { get; set; }
  }

  internal class BagReward
  {
    public List<ItemReward> Bag { get; set; }

    public ItemReward Take()
    {
      var roll = new Random();

      while (true)
      {
        foreach (var entry in Bag)
        {
          if (entry.Rate >= roll.Next(25))
            return entry;
        }
      }
    }
  }

  internal class ItemReward
  {
    public CapsuleRewardType Type { get; set; }

    public ItemNumber Item { get; set; }

    public ItemPriceType PriceType { get; set; }

    public ItemPeriodType PeriodType { get; set; }

    public byte Color { get; set; }

    public uint PEN { get; set; }

    public uint Period { get; set; }

    public uint[] Effects { get; set; }

    public uint Rate { get; set; }

    public uint Value { get; set; }
  }
}

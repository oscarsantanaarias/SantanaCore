using Santana.Database.Game;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Santana.Network.Services
{
    internal class CustomRuleRooms
    {
        public static ItemNumber[] PlasmaSword = new ItemNumber[] { 2006802, 2006803, 2006804, 2000095, 2000000, 2000001, 2000008, 2000024, 2000046, 2000047, 2000048, 2000069, 2000074, 2000080, 2000007, 2000067, 2000068 };
        public static ItemNumber[] CounterSword = new ItemNumber[] { 2000055, 2000002, 2000009, 2000015, 2000025, 2000033, 2000041, 2000051, 2000076, 2000084 };
        public static ItemNumber[] BAT = new ItemNumber[] { 2000082, 2000004, 2000019, 2000071, 2000028 };
        public static ItemNumber[] Katana = new ItemNumber[] { 2000054, 2000004, 2000018, 2000020, 2000022, 2000026, 2000072, 2000073 };
        public static ItemNumber[] VitalShock = new ItemNumber[] { 2000005, 2000063, 2000065 };
        public static ItemNumber[] Dagger = new ItemNumber[] { 2006901, 2006902, 2006903, 2006904, 2006905, 2006906, 2006907, 2006908, 2000006, 2000083, 2000011, 2000016, 2000027, 2000032, 2000037, 2000042, 2000037, 2000045, 2000059, 2000077, 2000081 };
        public static ItemNumber[] TwinBlade = new ItemNumber[] { 2000078, 2000010, 2000012, 2000034, 2000057, 2000058 };
        public static ItemNumber[] Breaker = new ItemNumber[] { 2000013, 2000014, 2000040, 2000079 };
        public static ItemNumber[] Fist = new ItemNumber[] { 2000036, 2000064, 2000038 };
        public static ItemNumber[] EXO = new ItemNumber[] { 2000031, 2000056, 2000075, 2000039, 2000029 };
        public static ItemNumber[] IronBoots = new ItemNumber[] { 2000030, 2000035 };
        public static ItemNumber[] SigmaBlade = new ItemNumber[] { 2000058, 2000017, 2000021, 2000023 };
        public static ItemNumber[] Sniper = new ItemNumber[] { 2030006, 2030008 };
        public static ItemNumber[] RailGun = new ItemNumber[] { 2030003, 2030007, 2030001, 2030009, 2030010 };
        public static ItemNumber[] Cannonade = new ItemNumber[] { 2030002, 2030004 };
        public static ItemNumber[] RocketLauncher = new ItemNumber[] { 2020010, 2020011 };
        public static ItemNumber[] LightMachineGun = new ItemNumber[] { 2010023, 2020008, 2020012, 2020007 };
        public static ItemNumber[] SubmachineGun = new ItemNumber[] { 2010009, 2010059, 2010000, 2010001, 2010017, 2010033, 2010037, 2010039, 2010058 };
        public static ItemNumber[] HandGun = new ItemNumber[] { 2060110, 2010007, 2010014, 2010027 };
        public static ItemNumber[] Shotgun = new ItemNumber[] { 2010013, 2010041, 2010008 };
        public static ItemNumber[] SentyNell = new ItemNumber[] { 2040003, 2040002 };
        public static ItemNumber[] Sentry = new ItemNumber[] { 2040001, 2040004, 2040005 };
        public static ItemNumber[] Heal = new ItemNumber[] { 2050005, 2050004, 2060001, 2060005, 2060006, 2060003, 2050008 };
        public static ItemNumber[] AirGun = new ItemNumber[] { 2010052, 2010016, 2010020 };
        public static ItemNumber[] MineGun = new ItemNumber[] { 2050001, 2050010 };
        public static ItemNumber[] MindShock = new ItemNumber[] { 2060004, 2060006, 2060002 };
        public static ItemNumber[] DualMagnum = new ItemNumber[] { 2010056, 2010050, 2010038, 2010028 };
        public static ItemNumber[] Turret = new ItemNumber[] { 2020006, 2020009, 2020005 };
        public static ItemNumber[] Revolver = new ItemNumber[] { 2010002, 2010010, 2010032, 2010036, 2010040, 2010054 };
        public static ItemNumber[] SemiRifle = new ItemNumber[] { 2010011, 2010029, 2010004 };
        public static ItemNumber[] SmashRifle = new ItemNumber[] { 2060108 , 2010006, 2010031, 2010035, 2010012, 2010042, 2010043 };
        public static ItemNumber[] AssaultRifle = new ItemNumber[] { 2010030, 2010049, 2010057, 2010024, 2010026 };
        public static ItemNumber[] BubblRifle = new ItemNumber[] { 2010021, 2010034, 2010025, 2010015, 2010051 };
        public static ItemNumber[] SparkRifle = new ItemNumber[] { 2010022, 2010018 };
        public static ItemNumber[] GaussRifle = new ItemNumber[] { 2020003, 2020002, 2020041, 2020040, 2020039, 2010015, 2010051 };
        public static ItemNumber[] EarthBomber = new ItemNumber[] { 2050012, 2050002, 2050006, 2050014, 2050015, 2050016 };
        public static ItemNumber[] LightBomber = new ItemNumber[] { 2050003, 2050013, 2050007 };
        public static ItemNumber[] HeavyMachine = new ItemNumber[] { 2020001, 2020004, 2020045, 2020042 };

        public static void SetCustomRules(Room room)
        {
            var title = room.Options.Name;

            if (!title.Contains("r:"))
                return;

            if (title.Contains("p"))
                room.Options.RulesAllowedItems.AddRange(PlasmaSword);

            if (title.Contains("c"))
                room.Options.RulesAllowedItems.AddRange(CounterSword);

            if (title.Contains("d"))
                room.Options.RulesAllowedItems.AddRange(Dagger);
        }

        public static bool CustomRules(Player plr)
        {
            try
            {
                if (plr == null)
                    return false;

                if (plr.Room.Options.GameRule == GameRule.Arcade || plr.Room.Options.GameRule == GameRule.Horde)
                    return true;

                var whitelist = plr.Room.Options.RulesAllowedItems;
                int offEnforcement = 0;

                foreach (var weapon in plr.CharacterManager.CurrentCharacter.Weapons.GetItems())
                {
                    if (weapon == null)
                        continue;
                    if (whitelist.Count() > 1)
                    {
                        if (!whitelist.Any(allowed => allowed == weapon?.ItemNumber))
                            offEnforcement++;
                    }
                }

                if (whitelist.Count() > 1 && offEnforcement > 0)
                    return false;
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            return true;
        }

        public static bool CustomRules(Player plr, byte CharSlot)
        {
            try
            {
                if (plr == null)
                    return false;

                if (plr.Room.Options.GameRule == GameRule.Arcade || plr.Room.Options.GameRule == GameRule.Horde)
                    return true;

                var whitelist = plr.Room.Options.RulesAllowedItems;
                int offEnforcement = 0;

                foreach (var weapon in plr.CharacterManager[CharSlot].Weapons.GetItems())
                {
                    if (weapon == null)
                        continue;
                    if (whitelist.Count() > 1)
                    {
                        if (!whitelist.Any(allowed => allowed == weapon?.ItemNumber))
                            offEnforcement++;
                    }
                }

                if (whitelist.Count() > 1 && offEnforcement > 0)
                    return false;
            }
            catch (Exception ex) { Console.WriteLine(ex); }
            return true;
        }
    }
}

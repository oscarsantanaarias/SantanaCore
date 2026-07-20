using System;
using System.Collections.Generic;
using System.Net;
using Santana;

namespace Santana
{
    internal class RoomCreationOptions
    {
        public string Name { get; set; }
        public GameRule GameRule { get; set; }
        public long Unk1 { get; set; }
        public TimeSpan TimeLimit { get; set; }
        public ushort ScoreLimit { get; set; }
        public string Password { get; set; }
        public bool IsFriendly { get; set; }
        public byte PlayerLimit { get; set; }
        public byte ItemLimit { get; set; }
        public bool IsNoIntrusion { get; set; }
        public bool HasSpectator { get; set; }
        public int SpectatorLimit { get; set; }
        public int Unk3 { get; set; }
        public int MapId { get; set; }
        public byte UniqueId { get; set; }
        public byte ChangeRuleId { get; set; }
        public bool IsBurning { get; set; }
        public bool IsWithoutStats { get; set; }
        public bool IsRandom { get; set; }
        public byte FMBURNMode => GetFMBurnModeInfo();
        public bool S5Mode { get; set; }
        public bool IsRandomMode { get; set; }
        public IPEndPoint ServerEndPoint { get; set; }
        public Player Creator { get; set; }
        public float HP { get; set; }
        public float MP { get; set; }
        public float Heal { get; set; }
        public int Ping { get; set; }
        public string CustomSettings { get; set; }
        public string FilterInfo { get; set; }
        public string Region { get; set; }
        public bool AllowChangeProfile { get; set; }
        public bool ActiveFilter { get; set; }
        public bool RandomSwapFilter { get; set; }
        public bool AutoSit { get; set; }

        public int CurrentFilterMode { get; set; }
        public int CurrentFilterMap { get; set; }

        public List<ItemNumber> RulesBlockedItems = new List<ItemNumber>();
        public List<ItemNumber> RulesAllowedItems = new List<ItemNumber>();
        public List<ItemNumber> RulesAllowedPerTeamItems = new List<ItemNumber>();

        public List<GameRule> ModeFilters = new List<GameRule>();
        public List<string> MapFilters = new List<string>();

        internal virtual byte GetFMBurnModeInfo()
        {
            if (IsWithoutStats)
                return IsFriendly ? (byte)5 : (byte)4;

            if (IsBurning)
                return IsFriendly ? (byte)3 : (byte)2;

            return IsFriendly ? (byte)1 : (byte)0;
        }
    }
}

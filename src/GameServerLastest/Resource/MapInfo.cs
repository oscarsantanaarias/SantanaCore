using System.Collections.Generic;
using SantanaLib.Configuration;

namespace Santana.Resource
{
  public class MapInfo
  {
    public MapInfo()
    {
      IsRandom = false;
    }

    public int Id { get; set; }
    public byte byteId { get; set; }
    public string Name { get; set; }
    public byte MinLevel { get; set; }
    public uint ServerId { get; set; }
    public uint ChannelId { get; set; }
    public byte RespawnType { get; set; }
    public int MaxPlayers { get; set; }
    public IniFile Config { get; set; }
    public bool IsRandom { get; set; }

    public GameRule GameRule { get; set; }

    public override string ToString()
    {
      return Name;
    }
  }
}

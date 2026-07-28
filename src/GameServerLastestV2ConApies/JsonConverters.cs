using System;
using System.Net;
using Newtonsoft.Json;

namespace Santana
{
  public class IPEndPointConverter : JsonConverter
  {
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      serializer.Serialize(writer, value.ToString());
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
    {
      var raw = serializer.Deserialize<string>(reader);
      var pieces = raw?.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);

      if (pieces != null && pieces.Length == 2 &&
          IPAddress.TryParse(pieces[0], out var host) &&
          int.TryParse(pieces[1], out var portNumber) &&
          portNumber >= IPEndPoint.MinPort && portNumber <= IPEndPoint.MaxPort)
      {
        return new IPEndPoint(host, portNumber);
      }

      Console.WriteLine($"[Config] Value '{raw}' could not be read as an ip:port pair");
      throw new JsonSerializationException($"Invalid endpoint '{raw}'");
    }

    public override bool CanConvert(Type objectType)
    {
      return objectType == typeof(IPEndPoint);
    }
  }

  public class TimeSpanConverter : JsonConverter
  {
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
      serializer.Serialize(writer, (uint)((TimeSpan)value).TotalMilliseconds);
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
        JsonSerializer serializer)
    {
      var millis = serializer.Deserialize<uint>(reader);
      return TimeSpan.FromMilliseconds(millis);
    }

    public override bool CanConvert(Type objectType)
    {
      return objectType == typeof(TimeSpan);
    }
  }
}

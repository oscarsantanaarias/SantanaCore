using Santana.Database.Auth;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Net;

namespace Santana
{
    public class IPChecker
    {
        internal int ProxyChecker(string context)
        {
            try
            {
                IPBanDto bannedEntry;
                using (var conn = AuthDatabase.Open())
                {
                    bannedEntry = DbUtil.Find<IPBanDto>(conn, statement => statement
                       .Where($"{nameof(IPBanDto.IP):C} = @{nameof(context)}")
                       .WithParameters(new { context })).FirstOrDefault();
                }

                if (bannedEntry != null && bannedEntry.IP == context)
                {
                    return 1;
                }

                var lookup = (HttpWebRequest)WebRequest.Create($"http://v2.api.iphub.info/ip/" + $"{context}");

                lookup.Method = "Get";
                lookup.Timeout = 12000;
                lookup.ContentType = "application/vnd.twitchtv.v5+json";
                lookup.Headers.Add("X-key", "==");

                using (var stream = lookup.GetResponse().GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream))
                {
                    var payload = JObject.Parse(reader.ReadToEnd());
                    return (int)payload["block"];
                }
            }
            catch { return 0; }
        }
    }
}

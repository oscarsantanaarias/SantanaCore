using System;
using System.Collections.Generic;
using System.Text;

using ProudNetSrc.Serialization;
namespace Santana.Network.Data.Club
{
    [Dto]
    public class ClubSearchResultDto
    {

        public int Id { get; set; }

        public string Icon { get; set; }

        public string Name { get; set; }

        public string OwnerName { get; set; }

        public ClubClass Class { get; set; }

        public uint Points { get; set; }

        public uint Unk { get; set; }

#if LATESTS4
        [Sec]
        public TimeSpan CreationDate { get; set; }
#else
        public string CreationDate { get; set; }
#endif

        public uint MemberCount { get; set; }

        public ClubArea Area { get; set; }

        public ClubActivity Activity { get; set; }

        public string Description { get; set; }
    }
}

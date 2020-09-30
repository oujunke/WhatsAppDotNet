using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WhatsAppLib.Models
{
    internal class MediaConnResponse
    {
        [JsonProperty("status")]
        public int Status { get; set; }
        [JsonProperty("media_conn")]
        public MediaConnModel MediaConn { get; set; }

        public class HostsModel
        {
            [JsonProperty("hostname")]
            public string Hostname { get; set; }
        }

        public class MediaConnModel
        {
            [JsonProperty("auth")]
            public string Auth { get; set; }
            [JsonProperty("ttl")]
            public int Ttl { get; set; }
            [JsonProperty("hosts")]
            public List<HostsModel> Hosts { get; set; }
        }
    }
}

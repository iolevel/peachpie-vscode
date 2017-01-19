using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class LogMessageParams
    {
        [JsonProperty("type")]
        public int Type { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class Diagnostic
    {
        [JsonProperty("range")]
        public Range Range { get; set; }

        [JsonProperty("severity")]
        public int? Severity { get; set; }

        [JsonProperty("code")]
        public object Code { get; set; }

        [JsonProperty("source")]
        public string Source { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}

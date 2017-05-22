using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class Hover
    {
        [JsonProperty("contents")]
        public MarkedString Contents { get; set; }

        [JsonProperty("range")]
        public Range Range { get; set; }
    }
}

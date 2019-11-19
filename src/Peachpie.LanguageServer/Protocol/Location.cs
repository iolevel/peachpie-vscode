using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    struct Location
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("range")]
        public Range Range { get; set; }
    }
}

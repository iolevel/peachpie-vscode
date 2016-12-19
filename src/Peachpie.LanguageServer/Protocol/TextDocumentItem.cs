using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class TextDocumentItem
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("languageId")]
        public string LanguageId { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    // TODO: Add also range and rangeLength for the incremental approach
    [JsonObject]
    internal class TextDocumentContentChangeEvent
    {
        [JsonProperty("text")]
        public string Text { get; set; }
    }
}

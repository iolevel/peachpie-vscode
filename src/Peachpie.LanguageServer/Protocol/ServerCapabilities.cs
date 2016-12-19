using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    // TODO: Add other capabilities if needed
    [JsonObject]
    internal class ServerCapabilities
    {
        [JsonProperty("textDocumentSync")]
        public int? TextDocumentSync { get; set; }
    }
}

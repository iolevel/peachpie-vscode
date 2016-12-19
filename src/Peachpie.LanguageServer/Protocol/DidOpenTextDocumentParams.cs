using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class DidOpenTextDocumentParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentItem TextDocument { get; set; }
    }
}

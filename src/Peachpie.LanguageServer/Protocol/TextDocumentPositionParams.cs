using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class TextDocumentPositionParams
    {
        [JsonProperty("textDocument")]
        public TextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("position")]
        public Position Position { get; set; }
    }
}

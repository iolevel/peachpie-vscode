using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class DidChangeTextDocumentParams
    {
        [JsonProperty("textDocument")]
        public VersionedTextDocumentIdentifier TextDocument { get; set; }

        [JsonProperty("contentChanges")]
        public TextDocumentContentChangeEvent[] ContentChanges { get; set; }
    }
}

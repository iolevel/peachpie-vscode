using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class InitializeParams
    {
        [JsonProperty("processId")]
        public int? ProcessId { get; set; }

        [JsonProperty("rootPath")]
        public string RootPath { get; set; }

        [JsonProperty("rootUri")]
        public string RootUri { get; set; }

        [JsonProperty("initializationOptions")]
        public JObject InitializationOptions { get; set; }

        [JsonProperty("capabilities")]
        public JObject Capabilities { get; set; }

        // TODO: Turn into enum ('off' | 'messages' | 'verbose')
        [JsonProperty("trace")]
        public string Trace { get; set; }
    }
}

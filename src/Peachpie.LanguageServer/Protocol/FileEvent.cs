using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    internal enum FileChangeType
    {
        Created = 1,
        Changed = 2,
        Deleted = 3
    }

    [JsonObject]
    internal class FileEvent
    {
        [JsonProperty("uri")]
        public string Uri { get; set; }

        [JsonProperty("type")]
        public FileChangeType Type { get; set; }
    }
}

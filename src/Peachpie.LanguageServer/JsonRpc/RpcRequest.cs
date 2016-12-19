using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.JsonRpc
{
    [JsonObject]
    internal class RpcRequest
    {
        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public JObject Params { get; set; }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.JsonRpc
{
    [JsonObject]
    internal class RpcResponse
    {
        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("result")]
        public object Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public object Error { get; set; }
    }

    [JsonObject]
    internal class RpcResponse<T>
    {
        [JsonProperty("id")]
        public object Id { get; set; }

        [JsonProperty("result")]
        public T Result { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public object Error { get; set; }
    }
}

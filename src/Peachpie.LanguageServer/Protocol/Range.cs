using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal struct Range
    {
        public Range(Position start, Position end)
        {
            this.Start = start;
            this.End = end;
        }

        [JsonProperty("start")]
        public Position Start { get; set; }

        [JsonProperty("end")]
        public Position End { get; set; }
    }
}

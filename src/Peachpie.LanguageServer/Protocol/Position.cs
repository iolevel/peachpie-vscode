using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal struct Position
    {
        public Position(int line, int character)
        {
            this.Line = line;
            this.Character = character;
        }

        [JsonProperty("line")]
        public int Line { get; set; }

        [JsonProperty("character")]
        public int Character { get; set; }
    }
}

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer.Protocol
{
    [JsonObject]
    internal class Hover
    {
        /// <summary>
        /// Elements are either of type <see cref="MarkedString"/> or <see cref="string"/>.
        /// </summary>
        [JsonProperty("contents")]
        public object[] Contents { get; set; }

        [JsonProperty("range")]
        public Range? Range { get; set; }
    }
}

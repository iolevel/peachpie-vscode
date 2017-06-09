using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer
{
    public class ToolTipInfo
    {
        public string Code { get; }

        public string Description { get; }

        public ToolTipInfo(string code, string description = null)
        {
            this.Code = code;
            this.Description = description;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;

namespace Peachpie.LanguageServer.Utils
{
    interface ILogTarget
    {
        void LogMessage(string message);
    }
}

using System;
using System.Linq;

namespace Peachpie.LanguageServer
{
    internal class ServerOptions
    {
        public bool IsDebug { get; }

        public ServerOptions(bool isDebug)
        {
            this.IsDebug = isDebug;
        }

        public static ServerOptions ParseFromArguments(string[] args)
        {
            bool isDebug = args.Contains("--debug");

            return new ServerOptions(isDebug);
        }
    }
}
using Peachpie.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            MainAsync(args).Wait();
        }

        private static async Task MainAsync(string[] args)
        {
            await EnvironmentUtils.InitializeAsync();

            var options = ServerOptions.ParseFromArguments(args);

            var requestReader = new MessageReader(Console.OpenStandardInput());
            var messageWriter = new MessageWriter(Console.OpenStandardOutput());

            var server = new PhpLanguageServer(options, requestReader, messageWriter);
            await server.Run();
        }
    }
}

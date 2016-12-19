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
            var requestReader = new MessageReader(Console.OpenStandardInput());
            var messageWriter = new MessageWriter(Console.OpenStandardOutput());

            while (true)
            {
                var request = await requestReader.ReadRequestAsync();

                switch (request.Method)
                {
                    case "initialize":
                        var initializeResult = new InitializeResult()
                        {
                            Capabilities = new ServerCapabilities()
                            {
                                // Full content synchronization
                                // TODO: Introduce an enum for this
                                TextDocumentSync = 1
                            }
                        };
                        messageWriter.WriteResponse(request.Id, initializeResult);

                        var showMessageParams = new ShowMessageParams()
                        {
                            Message = "Hello from Peachpie Language Server!",
                            // An information message
                            // TODO: Introduce an enum for this
                            Type = 3
                        };
                        messageWriter.WriteNotification("window/showMessage", showMessageParams);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}

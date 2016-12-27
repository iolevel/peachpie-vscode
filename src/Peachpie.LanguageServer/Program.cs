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
                        SendInitializationResponse(messageWriter, request);
                        if (args.Contains("--debug"))
                        {
                            SendGreetingMessage(messageWriter);
                        }
                        break;
                    case "textDocument/didOpen":
                        var openParams = request.Params.ToObject<DidOpenTextDocumentParams>();
                        SendMockDiagnostic(
                            messageWriter,
                            openParams.TextDocument.Uri,
                            openParams.TextDocument.Text);
                        break;
                    case "textDocument/didChange":
                        var changeParams = request.Params.ToObject<DidChangeTextDocumentParams>();
                        SendMockDiagnostic(
                            messageWriter,
                            changeParams.TextDocument.Uri,
                            changeParams.ContentChanges[0].Text);
                        break;
                    default:
                        break;
                }
            }
        }

        private static void SendInitializationResponse(MessageWriter messageWriter, JsonRpc.RpcRequest request)
        {
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
        }

        private static void SendGreetingMessage(MessageWriter messageWriter)
        {
            int processId = Process.GetCurrentProcess().Id;
            var showMessageParams = new ShowMessageParams()
            {
                Message = $"Hello from Peachpie Language Server! The ID of the process is {processId}",
                // An information message
                // TODO: Introduce an enum for this
                Type = 3
            };
            messageWriter.WriteNotification("window/showMessage", showMessageParams);
        }

        private static void SendMockDiagnostic(MessageWriter messageWriter, string uri, string text)
        {
            int endLine = text.IndexOf('\n');
            if (endLine == -1)
            {
                return;
            }

            var diagnosticsParams = new PublishDiagnosticsParams()
            {
                Uri = uri,
                Diagnostics = new[]
                {
                    new Diagnostic()
                    {
                        Range = new Range(new Position(0, 0), new Position(0, endLine)),
                        // Warning
                        // TODO: Introduce an enum for this
                        Severity = 2,
                        Code = "MOCK001",
                        Source = "peachpie",
                        Message = "I have a bad feeling about this"
                    }
                }
            };

            messageWriter.WriteNotification("textDocument/publishDiagnostics", diagnosticsParams);
        }
    }
}

using Peachpie.LanguageServer.Protocol;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    internal class PhpLanguageServer
    {
        private ServerOptions _options;
        private MessageReader _requestReader;
        private MessageWriter _messageWriter;

        public PhpLanguageServer(ServerOptions options, MessageReader requestReader, MessageWriter messageWriter)
        {
            _options = options;
            _requestReader = requestReader;
            _messageWriter = messageWriter;
        }

        public async Task Run()
        {
            while (true)
            {
                var request = await _requestReader.ReadRequestAsync();

                switch (request.Method)
                {
                    case "initialize":
                        SendInitializationResponse(request);
                        if (_options.IsDebug)
                        {
                            SendGreetingMessage();
                        }
                        break;
                    case "textDocument/didOpen":
                        var openParams = request.Params.ToObject<DidOpenTextDocumentParams>();
                        SendMockDiagnostic(openParams.TextDocument.Uri, openParams.TextDocument.Text);
                        break;
                    case "textDocument/didChange":
                        var changeParams = request.Params.ToObject<DidChangeTextDocumentParams>();
                        SendMockDiagnostic(changeParams.TextDocument.Uri, changeParams.ContentChanges[0].Text);
                        break;
                    default:
                        break;
                }
            }
        }

        private void SendInitializationResponse(JsonRpc.RpcRequest request)
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
            _messageWriter.WriteResponse(request.Id, initializeResult);
        }


        private void SendGreetingMessage()
        {
            int processId = Process.GetCurrentProcess().Id;
            var showMessageParams = new ShowMessageParams()
            {
                Message = $"Hello from Peachpie Language Server! The ID of the process is {processId}",
                // An information message
                // TODO: Introduce an enum for this
                Type = 3
            };
            _messageWriter.WriteNotification("window/showMessage", showMessageParams);
        }

        private void SendMockDiagnostic(string uri, string text)
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

            _messageWriter.WriteNotification("textDocument/publishDiagnostics", diagnosticsParams);
        }
    }
}
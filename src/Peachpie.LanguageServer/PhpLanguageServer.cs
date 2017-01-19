using Newtonsoft.Json;
using Peachpie.LanguageServer.Protocol;
using System;
using System.Diagnostics;
using System.IO;
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
                if (_options.IsDebug)
                {
                    SendLogMessage($"Received: {JsonConvert.SerializeObject(request)}");
                }

                switch (request.Method)
                {
                    case "initialize":
                        var initializeParams = request.Params.ToObject<InitializeParams>();
                        SendInitializationResponse(request);
                        if (_options.IsDebug)
                        {
                            SendGreetingMessage();
                        }
                        OpenFolder(initializeParams.RootPath);
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

        private void OpenFolder(string rootPath)
        {
            if (rootPath == null)
            {
                return;
            }

            // TODO: Determine the right suffixes by inspecting project.json
            var sourceFiles = Directory.GetFiles(rootPath, "*.php", SearchOption.AllDirectories);
            foreach (var sourceFile in sourceFiles)
            {
                string uri = new Uri(sourceFile).ToString()         // For file:/// prefix and forward slashes
                string sourceText = File.ReadAllText(sourceFile);
                SendMockDiagnostic(uri, sourceText);
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

        private void SendLogMessage(string text)
        {
            var logMessageParams = new LogMessageParams()
            {
                Message = text,
                // A log message
                // TODO: Introdue an enum for this
                Type = 4
            };
            _messageWriter.WriteNotification("window/logMessage", logMessageParams);
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
                        Message = $"I have a bad feeling about this file ({uri})"
                    }
                }
            };

            _messageWriter.WriteNotification("textDocument/publishDiagnostics", diagnosticsParams);
        }
    }
}
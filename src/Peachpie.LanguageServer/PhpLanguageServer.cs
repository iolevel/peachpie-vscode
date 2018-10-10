using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using Pchp.CodeAnalysis;
using Peachpie.LanguageServer.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Peachpie.LanguageServer
{
    internal class PhpLanguageServer
    {
        private const string DiagnosticSource = "peachpie";

        private ServerOptions _options;
        private MessageReader _requestReader;
        private MessageWriter _messageWriter;

        private string _rootPath;
        private ProjectHandler _project;

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
                //if (_options.IsDebug)
                //{
                //    SendLogMessage($"Received: {JsonConvert.SerializeObject(request)}");
                //}

                switch (request.Method)
                {
                    case "initialize":
                        var initializeParams = request.Params.ToObject<InitializeParams>();
                        SendInitializationResponse(request);
                        await OpenFolder(initializeParams.RootPath);
                        break;
                    case "initialized":
                        // ignore
                        break;
                    case "textDocument/didOpen":
                        var openParams = request.Params.ToObject<DidOpenTextDocumentParams>();
                        // TODO: Decide how to handle opened files that are not in the current folder
                        break;
                    case "textDocument/didChange":
                        var changeParams = request.Params.ToObject<DidChangeTextDocumentParams>();
                        await ProcessDocumentChanges(changeParams);
                        break;
                    case "workspace/didChangeWatchedFiles":
                        var changeWatchedParams = request.Params.ToObject<DidChangeWatchedFilesParams>();
                        await ProcessFileChangesAsync(changeWatchedParams);
                        break;
                    case "textDocument/hover":
                        var hoverParams = request.Params.ToObject<TextDocumentPositionParams>();
                        ProcessHover(request.Id, hoverParams);
                        break;
                    default:
                        if (request.Method.StartsWith("$/"))
                        {
                            // ignored
                            break;
                        }

                        SendLogMessage($"Request '{request.Method}' was unhandled.");
                        break;
                }
            }
        }

        private async Task OpenFolder(string rootPath)
        {
            if (rootPath == null)
            {
                return;
            }

            _rootPath = PathUtils.NormalizePath(rootPath);

            await TryReloadProjectAsync();
        }

        private async Task ProcessFileChangesAsync(DidChangeWatchedFilesParams changeWatchedParams)
        {
            // We now watch only .msbuildproj and project.assets.json files, forcing us to reload the project
            await TryReloadProjectAsync();
        }

        private async Task TryReloadProjectAsync()
        {
            if (_rootPath == null)
            {
                return;
            }

            var newProject = await ProjectUtils.TryGetFirstPhpProjectAsync(_rootPath);
            if (newProject == null)
            {
                return;
            }

            if (_project != null)
            {
                _project.DocumentDiagnosticsChanged -= DocumentDiagnosticsChanged;
            }

            _project = newProject;
            _project.DocumentDiagnosticsChanged += DocumentDiagnosticsChanged;
            _project.Initialize();
        }

        private async Task ProcessDocumentChanges(DidChangeTextDocumentParams changeParams)
        {
            if (_project == null)
            {
                await TryReloadProjectAsync();
            }

            if (_project != null)
            {
                string path = PathUtils.NormalizePath(changeParams.TextDocument.Uri);

                // Don't care about the documents outside the current folder if it's opened
                if (_rootPath != null && !path.StartsWith(_rootPath))
                {
                    return;
                }

                // Similarly, ignore files outside the active project if opened
                if (!path.StartsWith(_project.RootPath))
                {
                    return;
                }

                // For now, only the full document synchronization works
                string text = changeParams.ContentChanges[0].Text;

                _project.UpdateFile(path, text);
            }
        }

        private void ProcessHover(object requestId, TextDocumentPositionParams hoverParams)
        {
            ToolTipInfo tooltip = null;
            if (_project != null)
            {
                string filepath = PathUtils.NormalizePath(hoverParams.TextDocument.Uri);
                tooltip = _project.ObtainToolTip(filepath, hoverParams.Position.Line, hoverParams.Position.Character);
            }

            Hover response;
            if (tooltip != null)
            {
                var codePart = new MarkedString()
                {
                    Language = "php",
                    Value = tooltip.Code
                };

                response = new Hover()
                {
                    Contents = (tooltip.Description != null) ?
                        new object[] { codePart, tooltip.Description }
                        : new object[] { codePart }
                };
            }
            else
            {
                // Return empty response to hide the "Loading..." text in the box
                response = new Hover()
                {
                    Contents = new object[] { }
                };
            }
            _messageWriter.WriteResponse(requestId, response);
        }

        private void SendInitializationResponse(JsonRpc.RpcRequest request)
        {
            var initializeResult = new InitializeResult()
            {
                Capabilities = new ServerCapabilities()
                {
                    // Full content synchronization
                    // TODO: Introduce an enum for this
                    TextDocumentSync = 1,
                    HoverProvider = true
                }
            };
            _messageWriter.WriteResponse(request.Id, initializeResult);

            SendLogMessage($@"
PeachPie Language Server
  PID: {Process.GetCurrentProcess().Id}
  Path: {System.Reflection.Assembly.GetEntryAssembly().Location}
");

            if (_options.IsDebug)
            {
                SendGreetingMessage();
            }
        }

        private void SendGreetingMessage()
        {
            int processId = Process.GetCurrentProcess().Id;
            var showMessageParams = new ShowMessageParams()
            {
                Message = $"Hello from PeachPie Language Server! The ID of the process is {processId}",
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
                // TODO: Introduce an enum for this
                Type = 4
            };
            _messageWriter.WriteNotification("window/logMessage", logMessageParams);
        }

        private void DocumentDiagnosticsChanged(object sender, ProjectHandler.DocumentDiagnosticsEventArgs e)
        {
            var diagnosticsParams = new PublishDiagnosticsParams()
            {
                Uri = new Uri(e.DocumentPath).AbsoluteUri,
                Diagnostics = e.Diagnostics
                    .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                    .Select(diagnostic =>
                    new Protocol.Diagnostic()
                    {
                        Range = ConvertLocation(diagnostic.Location),
                        Severity = ConvertSeverity(diagnostic.Severity),
                        Code = diagnostic.Id,
                        Source = diagnostic.Id,
                        Message = diagnostic.GetMessage(),
                    }).ToArray()
            };

            _messageWriter.WriteNotification("textDocument/publishDiagnostics", diagnosticsParams);
        }

        private static Range ConvertLocation(Location location)
        {
            var lineSpan = location.GetLineSpan();
            return new Range(
                new Position(lineSpan.StartLinePosition.Line, lineSpan.StartLinePosition.Character),
                new Position(lineSpan.EndLinePosition.Line, lineSpan.EndLinePosition.Character));
        }

        private static int? ConvertSeverity(DiagnosticSeverity severity)
        {
            // TODO: Introduce an enum for this
            switch (severity)
            {
                case DiagnosticSeverity.Error:
                    return 1;
                case DiagnosticSeverity.Warning:
                    return 2;
                case DiagnosticSeverity.Info:
                    return 3;
                case DiagnosticSeverity.Hidden:
                default:
                    return null;
            }
        }
    }
}
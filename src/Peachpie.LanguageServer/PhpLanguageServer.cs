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

        static Version LatestPeachpieVersion
        {
            get
            {
                var v4 = typeof(PhpCompilation).Assembly.GetName().Version;
                return new Version(v4.Major, v4.Minor, v4.Build); // v3
            }
        }

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
                    // initialize

                    case "initialize":
                        var initializeParams = request.Params.ToObject<InitializeParams>();
                        SendInitializationResponse(request);
                        await OpenFolder(initializeParams.RootPath);
                        break;
                    case "initialized":
                        // ignore
                        break;

                    // shutdown/exit

                    case "shutdown":
                        _project?.Dispose();
                        _messageWriter.WriteResponse<object>(request.Id, null); // empty response
                        break;  // not exit the process

                    case "exit":
                        _project?.Dispose();
                        return; // exit the process

                    // workspace

                    case "workspace/didChangeWatchedFiles":
                        var changeWatchedParams = request.Params.ToObject<DidChangeWatchedFilesParams>();
                        await ProcessFileChangesAsync(changeWatchedParams);
                        break;

                    // textDocument

                    case "textDocument/didOpen":
                        var openParams = request.Params.ToObject<DidOpenTextDocumentParams>();
                        // TODO: Decide how to handle opened files that are not in the current folder
                        break;
                    case "textDocument/didChange":
                        await ProcessDocumentChanges(request.Params.ToObject<DidChangeTextDocumentParams>());
                        break;
                    case "textDocument/hover":
                        ProcessHover(request.Id, request.Params.ToObject<TextDocumentPositionParams>());
                        break;
                    case "textDocument/definition":
                        ProcessGoToDefinition(request.Id, request.Params.ToObject<TextDocumentPositionParams>());
                        break;
                    case "textDocument/didClose":
                        // ignored
                        break;

                    //

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

            ProjectHandler newProject = null;

            try
            {
                newProject = await ProjectUtils.TryGetFirstPhpProjectAsync(_rootPath);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString());
            }

            if (newProject == null)
            {
                return;
            }

            // dispose previous one
            _project?.Dispose();

            // new project
            _project = newProject;
            _project.DocumentDiagnosticsChanged += DocumentDiagnosticsChanged;
            _project.Initialize();

            // check the Sdk version
            newProject.TryGetSdkVersion(out var sdkverstr);

            SendLogMessage($@"Loaded project:
  {newProject.BuildInstance.FullPath}
  PeachPie Version: {(sdkverstr ?? "unknown")}");

            if (Version.TryParse(sdkverstr, out var sdkver) && sdkver < LatestPeachpieVersion)
            {
                SendLogMessage($"  New version available: {LatestPeachpieVersion}");
                ShowMessage($"PeachPie {LatestPeachpieVersion} is available! Your project is running version {sdkver.ToString(3)}, please update.");
            }
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

                try
                {
                    _project.UpdateFile(path, text);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString());
                }
            }
        }

        private void ProcessGoToDefinition(object requestId, TextDocumentPositionParams docPosition)
        {
            // result: Location | Location[] | LocationLink[] | null

            Protocol.Location[] result = null;

            if (_project != null)
            {
                string filepath = PathUtils.NormalizePath(docPosition.TextDocument.Uri);
                try
                {
                    var locations = _project.ObtainDefinition(filepath, docPosition.Position.Line, docPosition.Position.Character);
                    result = locations?.ToArray();
                }
                catch (NotImplementedException) { } // might not be implemented
                catch (NotSupportedException) { }
                catch (AggregateException) { }
            }

            _messageWriter.WriteResponse(requestId, result);
        }

        private void ProcessHover(object requestId, TextDocumentPositionParams hoverParams)
        {
            ToolTipInfo tooltip;

            if (_project != null)
            {
                string filepath = PathUtils.NormalizePath(hoverParams.TextDocument.Uri);
                tooltip = _project.ObtainToolTip(filepath, hoverParams.Position.Line, hoverParams.Position.Character);
            }
            else
            {
                tooltip = null;
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
                response = null;
            }

            _messageWriter.WriteResponse(requestId, response);
        }

        private void SendInitializationResponse(JsonRpc.RpcRequest request)
        {
            var initializeResult = new InitializeResult()
            {
                Capabilities = new ServerCapabilities()
                {
                    TextDocumentSync = TextDocumentSyncKind.Full,   // TOOD: change to incremental to improve perf.
                    HoverProvider = true,
                    DefinitionProvider = true,
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
            ShowMessage($"Hello from PeachPie Language Server! The ID of the process is {processId}");
        }

        private void ShowMessage(string message)
        {
            var showMessageParams = new ShowMessageParams()
            {
                Message = message,
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
                        Range = diagnostic.Location.AsRange(),
                        Severity = ConvertSeverity(diagnostic.Severity),
                        Code = diagnostic.Id,
                        Source = "PeachPie",
                        Message = diagnostic.GetMessage(),
                    }).ToArray()
            };

            _messageWriter.WriteNotification("textDocument/publishDiagnostics", diagnosticsParams);
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
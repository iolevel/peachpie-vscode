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
        private const string ParserDiagnosticSource = "pchpp";
        private const string CompilerDiagnosticSource = "pchpc";

        private ServerOptions _options;
        private MessageReader _requestReader;
        private MessageWriter _messageWriter;

        private string _rootPath;
        private CompilationDiagnosticBroker _diagnosticBroker;
        private HashSet<string> _filesWithParserErrors = new HashSet<string>();
        private HashSet<string> _filesWithSemanticDiagnostics = new HashSet<string>();

        public PhpLanguageServer(ServerOptions options, MessageReader requestReader, MessageWriter messageWriter)
        {
            _options = options;
            _requestReader = requestReader;
            _messageWriter = messageWriter;
            _diagnosticBroker = new CompilationDiagnosticBroker(this.HandleCompilationDiagnostics);
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
                        await OpenFolder(initializeParams.RootPath);
                        break;
                    case "textDocument/didOpen":
                        var openParams = request.Params.ToObject<DidOpenTextDocumentParams>();
                        // TODO: Decide how to handle opened files that are not in the current folder
                        break;
                    case "textDocument/didChange":
                        var changeParams = request.Params.ToObject<DidChangeTextDocumentParams>();
                        ProcessDocumentChanges(changeParams);
                        break;
                    default:
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

            var projectResult = await ProjectUtils.TryGetFirstPhpProjectAsync(rootPath);
            if (projectResult.Compilation == null)
            {
                return;
            }

            rootPath = Path.GetDirectoryName(projectResult.ProjectPath);

            _diagnosticBroker.UpdateCompilation(projectResult.Compilation);
            _rootPath = PathUtils.NormalizePath(rootPath);

            // TODO: Determine the right suffixes by inspecting the MSBuild project
            var sourceFiles = Directory.GetFiles(rootPath, "*.php", SearchOption.AllDirectories);
            foreach (var sourceFile in sourceFiles)
            {
                string path = PathUtils.NormalizePath(sourceFile);
                string text = File.ReadAllText(sourceFile);
                UpdateFile(path, text);
            }
        }

        private void ProcessDocumentChanges(DidChangeTextDocumentParams changeParams)
        {
            // For now, only the full document synchronization works
            string path = PathUtils.NormalizePath(changeParams.TextDocument.Uri);

            // Do not care about the documents outside of the current folder if it's opened
            if (_rootPath != null && !path.StartsWith(_rootPath))
            {
                return;
            }

            string text = changeParams.ContentChanges[0].Text;
            UpdateFile(path, text);
        }

        private void UpdateFile(string path, string text)
        {
            var syntaxTree = PhpSyntaxTree.ParseCode(text, PhpParseOptions.Default, PhpParseOptions.Default, path);
            if (syntaxTree.Diagnostics.Length > 0)
            {
                _filesWithParserErrors.Add(path);
                SendDocumentDiagnostics(path, ParserDiagnosticSource, syntaxTree.Diagnostics);
            }
            else
            {
                if (_filesWithParserErrors.Remove(path))
                {
                    // If there were any errors previously, send an empty set to remove them
                    SendDocumentDiagnostics(path, ParserDiagnosticSource, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
                }

                // Update in the compilation
                if (_diagnosticBroker.Compilation != null)
                {
                    PhpCompilation updatedCompilation;
                    var currentTree = _diagnosticBroker.Compilation.SyntaxTrees
                        .OfType<PhpSyntaxTree>()
                        .FirstOrDefault(tree => tree.FilePath == path);
                    if (currentTree == null)
                    {
                        updatedCompilation = (PhpCompilation)_diagnosticBroker.Compilation.AddSyntaxTrees(syntaxTree);
                    }
                    else
                    {
                        updatedCompilation = (PhpCompilation)_diagnosticBroker.Compilation.ReplaceSyntaxTree(currentTree, syntaxTree);
                    }

                    _diagnosticBroker.UpdateCompilation(updatedCompilation); 
                }
            }
        }

        private void HandleCompilationDiagnostics(IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        {
            var errorFiles = new HashSet<string>();

            var fileGroups = diagnostics.GroupBy(diagnostic => diagnostic.Location.SourceTree.FilePath);
            foreach (var fileDiagnostics in fileGroups)
            {
                errorFiles.Add(fileDiagnostics.Key);
                this.SendDocumentDiagnostics(fileDiagnostics.Key, CompilerDiagnosticSource, fileDiagnostics);
            }

            var cleared = _filesWithSemanticDiagnostics.Except(errorFiles);
            foreach (var file in cleared)
            {
                this.SendDocumentDiagnostics(file, CompilerDiagnosticSource, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
            }

            _filesWithSemanticDiagnostics = errorFiles;
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

        private void SendDocumentDiagnostics(string path, string source, IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        {
            var diagnosticsParams = new PublishDiagnosticsParams()
            {
                Uri = new Uri(path).AbsoluteUri,
                Diagnostics = diagnostics
                    .Where(diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                    .Select(diagnostic =>
                    new Protocol.Diagnostic()
                    {
                        Range = ConvertLocation(diagnostic.Location),
                        Severity = ConvertSeverity(diagnostic.Severity),
                        Code = diagnostic.Id,
                        Source = "peachpie",
                        Message = diagnostic.GetMessage()
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
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ProjectModel;
using Newtonsoft.Json;
using NuGet.Frameworks;
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
        private const string DefaultConfiguration = "Debug";
        private static readonly NuGetFramework DefaultFramework = FrameworkConstants.CommonFrameworks.NetCoreApp10;

        private ServerOptions _options;
        private MessageReader _requestReader;
        private MessageWriter _messageWriter;

        private PhpCompilation _compilation;
        private HashSet<string> _filesWithParserErrors = new HashSet<string>();

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

        private void OpenFolder(string rootPath)
        {
            if (rootPath == null)
            {
                return;
            }

            string projectFile = Path.Combine(rootPath, Project.FileName);
            if (!File.Exists(projectFile))
            {
                return;
            }

            this._compilation = CreateCompilationFromProject(projectFile);

            // TODO: Determine the right suffixes by inspecting project.json
            var sourceFiles = Directory.GetFiles(rootPath, "*.php", SearchOption.AllDirectories);
            foreach (var sourceFile in sourceFiles)
            {
                string uri = new Uri(sourceFile).ToString();         // For file:/// prefix and forward slashes
                string text = File.ReadAllText(sourceFile);
                UpdateFile(uri, text);
            }
        }

        private void ProcessDocumentChanges(DidChangeTextDocumentParams changeParams)
        {
            // For now, only the full document synchronization works
            string uri = changeParams.TextDocument.Uri;
            string text = changeParams.ContentChanges[0].Text;
            UpdateFile(uri, text);
        }

        private void UpdateFile(string uri, string text)
        {
            var syntaxTree = PhpSyntaxTree.ParseCode(text, PhpParseOptions.Default, PhpParseOptions.Default, uri);
            if (syntaxTree.Diagnostics.Length > 0)
            {
                _filesWithParserErrors.Add(uri);
                SendDocumentDiagnostics(uri, syntaxTree.Diagnostics);
            }
            else
            {
                if (_filesWithParserErrors.Remove(uri))
                {
                    // If there were any errors previously, send an empty set to remove them
                    SendDocumentDiagnostics(uri, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
                }

                // TODO: Add to the compilation
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

        private void SendDocumentDiagnostics(string uri, IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        {
            var diagnosticsParams = new PublishDiagnosticsParams()
            {
                Uri = uri,
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

        private static PhpCompilation CreateCompilationFromProject(string projectFile)
        {
            var projectContext = ProjectContext.Create(projectFile, DefaultFramework);
            var exporter = projectContext.CreateExporter(DefaultConfiguration);
            var libraryExports = exporter.GetDependencies().ToArray();
            var metadataReferences = libraryExports
                .SelectMany(lib => lib.CompilationAssemblies)
                .Select(asset => MetadataReference.CreateFromFile(asset.ResolvedPath))
                .ToArray();
            var options = new PhpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                baseDirectory: projectContext.ProjectDirectory,
                sdkDirectory: null);

            var compilation = PhpCompilation.Create(
                projectContext.ProjectFile.Name,
                ImmutableArray<SyntaxTree>.Empty,
                metadataReferences,
                options);

            return compilation;
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
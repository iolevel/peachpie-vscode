using Microsoft.Build.Execution;
using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Symbols;
using Devsense.PHP.Syntax.Ast;
using Devsense.PHP.Text;
using Pchp.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Peachpie.LanguageServer
{
    class ProjectHandler : IDisposable
    {
        public class DocumentDiagnosticsEventArgs : EventArgs
        {
            public string DocumentPath { get; }

            public IEnumerable<Diagnostic> Diagnostics { get; }

            public DocumentDiagnosticsEventArgs(string documentPath, IEnumerable<Diagnostic> diagnostics)
            {
                this.DocumentPath = documentPath;
                this.Diagnostics = diagnostics;
            }
        }

        readonly CompilationDiagnosticBroker _diagnosticBroker;

        readonly HashSet<string> _filesWithParserErrors = new HashSet<string>();

        private HashSet<string> _filesWithSemanticDiagnostics = new HashSet<string>();

        public PhpCompilation Compilation => _diagnosticBroker.Compilation;

        public ProjectInstance BuildInstance { get; }

        public string RootPath => PathUtils.NormalizePath(BuildInstance.Directory);

        public event EventHandler<DocumentDiagnosticsEventArgs> DocumentDiagnosticsChanged;

        public ProjectHandler(PhpCompilation compilation, ProjectInstance buildInstance)
        {
            BuildInstance = buildInstance;
            _diagnosticBroker = new CompilationDiagnosticBroker(HandleCompilationDiagnostics);
            _diagnosticBroker.UpdateCompilation(compilation);
        }

        public void Initialize()
        {
            // Initially populate _filesWithParserErrors and send the corresponding diagnostics
            // (_filesWithSemanticDiagnostics will be updated by _diagnosticBroker)
            var diagnostics = Compilation.GetParseDiagnostics();
            foreach (var fileDiagnostics in diagnostics.GroupBy(diag => diag.Location.SourceTree.FilePath))
            {
                string path = fileDiagnostics.Key;
                _filesWithParserErrors.Add(path);
                OnDocumentDiagnosticsChanged(path, fileDiagnostics);
            }
        }

        public void UpdateFile(string path, string text)
        {
            var syntaxTree = PhpSyntaxTree.ParseCode(SourceText.From(text, Encoding.UTF8), PhpParseOptions.Default, PhpParseOptions.Default, path);
            if (syntaxTree.Diagnostics.Length > 0)
            {
                _filesWithParserErrors.Add(path);
                OnDocumentDiagnosticsChanged(path, syntaxTree.Diagnostics);
            }
            else
            {
                if (_filesWithParserErrors.Remove(path))
                {
                    // If there were any errors previously, send an empty set to remove them
                    OnDocumentDiagnosticsChanged(path, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
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

        internal IEnumerable<Protocol.Location> ObtainDefinition(string filepath, int line, int character)
        {
            var compilation = _diagnosticBroker.LastAnalysedCompilation;

            // We have to work with already fully analyzed and bound compilation that is up-to-date with the client's code
            if (compilation == null ||
                compilation != _diagnosticBroker.Compilation)
            {
                return Array.Empty<Protocol.Location>();
            }

            // Find the symbols gathered from the given source code
            return ToolTipUtils.ObtainDefinition(compilation, filepath, line, character);
        }

        public ToolTipInfo ObtainToolTip(string filepath, int line, int character)
        {
            var compilation = _diagnosticBroker.LastAnalysedCompilation;

            // We have to work with already fully analyzed and bound compilation that is up-to-date with the client's code
            if (compilation == null ||
                compilation != _diagnosticBroker.Compilation)
            {
                return null;
            }

            // Find the symbols gathered from the given source code
            return ToolTipUtils.ObtainToolTip(compilation, filepath, line, character);
        }

        private void HandleCompilationDiagnostics(IEnumerable<Microsoft.CodeAnalysis.Diagnostic> diagnostics)
        {
            var errorFiles = new HashSet<string>();

            var fileGroups = diagnostics.GroupBy(diagnostic => diagnostic.Location.SourceTree.FilePath);
            foreach (var fileDiagnostics in fileGroups)
            {
                errorFiles.Add(fileDiagnostics.Key);
                OnDocumentDiagnosticsChanged(fileDiagnostics.Key, fileDiagnostics);
            }

            var cleared = _filesWithSemanticDiagnostics.Except(errorFiles);
            foreach (var file in cleared)
            {
                OnDocumentDiagnosticsChanged(file, ImmutableArray<Microsoft.CodeAnalysis.Diagnostic>.Empty);
            }

            _filesWithSemanticDiagnostics = errorFiles;
        }

        private void OnDocumentDiagnosticsChanged(string documentPath, IEnumerable<Diagnostic> diagnostics)
        {
            DocumentDiagnosticsChanged?.Invoke(this, new DocumentDiagnosticsEventArgs(documentPath, diagnostics));
        }

        /// <summary>
        /// Gets used PeachPie Sdk version.
        /// </summary>
        public bool TryGetSdkVersion(out string version)
        {
            version = this.BuildInstance.GetPropertyValue("PeachpieVersion");
            return version != null;
        }

        public void Dispose()
        {
            this.DocumentDiagnosticsChanged = null;
        }
    }
}

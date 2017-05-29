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
    public class ProjectHandler
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

        private CompilationDiagnosticBroker _diagnosticBroker;
        private HashSet<string> _filesWithParserErrors = new HashSet<string>();
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
            var syntaxTree = PhpSyntaxTree.ParseCode(text, PhpParseOptions.Default, PhpParseOptions.Default, path);
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

        public string ObtainToolTip(string filepath, int line, int character)
        {
            // We have to work with already fully analyzed and bound compilation that is up-to-date with the client's code
            if (_diagnosticBroker.LastAnalysedCompilation == null
                || _diagnosticBroker.LastAnalysedCompilation != _diagnosticBroker.Compilation)
            {
                return null;
            }

            // Find the symbols gathered from the given source code
            var compilation = _diagnosticBroker.LastAnalysedCompilation;
            var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filepath);
            if (tree == null)
            {
                return null;
            }

            // Get the position in the file
            int position = tree.GetPosition(new LinePosition(line, character));
            if (position == -1)
            {
                return null;
            }

            // Find the bound node corresponding to the text position
            SourceSymbolSearcher.SymbolStat searchResult = null;
            foreach (var routine in compilation.GetUserDeclaredRoutinesInFile(tree))
            {
                // Consider only routines containing the position being searched (<Main> has span [0..0])
                if (routine.IsGlobalScope || routine.GetSpan().Contains(position))
                {
                    // Search parameters at first
                    searchResult = SourceSymbolSearcher.SearchParameters(routine, position);
                    if (searchResult != null)
                    {
                        break;
                    }

                    // Search the routine body
                    searchResult = SourceSymbolSearcher.SearchCFG(routine.ControlFlowGraph, position);
                    if (searchResult != null)
                    {
                        break;
                    }
                }
            }

            if (searchResult == null)
            {
                return null;
            }

            return FormulateToolTip(searchResult);
        }

        /// <summary>
        /// Creates the text of a hint. Either <paramref name="symbol"/> or <paramref name="operation"/> is expected.
        /// </summary>
        private string FormulateToolTip(SourceSymbolSearcher.SymbolStat searchResult)
        {
            if (searchResult.BoundExpression is BoundVariableRef varRef && varRef.Name.IsDirect)
            {
                // Usage of variable or parameter
                var variableKind = varRef.Variable.VariableKind;
                string name = varRef.Name.NameValue.Value;
                var typeMask = varRef.TypeRefMask;

                return FormulateVariableToolTip(searchResult.TypeCtx, variableKind, name, typeMask);
            }
            else if (searchResult.Symbol is IParameterSymbol parameter)
            {
                // Parameter definition
                var variableKind = VariableKind.Parameter;
                string name = searchResult.Symbol.Name;
                TypeRefMask typeMask = ((IPhpValue)parameter).GetResultType(searchResult.TypeCtx);

                return FormulateVariableToolTip(searchResult.TypeCtx, variableKind, name, typeMask);
            }
            else
            {
                return null;
            }
        }

        private static string FormulateVariableToolTip(TypeRefContext typeContext, VariableKind variableKind, string name, TypeRefMask typeRefMask)
        {
            var text = new StringBuilder();

            switch (variableKind)
            {
                case VariableKind.LocalVariable:
                    text.Append("(var) ");
                    break;
                case VariableKind.GlobalVariable:
                    text.Append("(global) ");
                    break;
                case VariableKind.Parameter:
                    text.Append("(parameter) ");
                    break;
                case VariableKind.StaticVariable:
                    text.Append("(static local) ");
                    break;
                case VariableKind.ThisParameter:
                default:
                    break;
            }

            string types = typeContext.ToString(typeRefMask);
            text.Append($"${name} : {types}");

            return text.ToString();
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
    }
}

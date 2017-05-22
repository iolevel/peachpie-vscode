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

        public string ObtainHoverHint(string filepath, int line, int character)
        {
            // We have to work with already fully analyzed and bound compilation that is up-to-date with the client's code
            if (_diagnosticBroker.LastAnalysedCompilation == null
                || _diagnosticBroker.LastAnalysedCompilation != _diagnosticBroker.Compilation)
            {
                return null;
            }

            // Find the symbols gathered from the given source code
            var compilation = _diagnosticBroker.LastAnalysedCompilation;
            string relativePath = PhpFileUtilities.GetRelativePath(filepath, this.RootPath);
            var boundFile = compilation.SourceSymbolCollection.GetFile(relativePath);
            if (boundFile == null)
            {
                return null;
            }

            var lineBreaks = boundFile.SyntaxTree.Source.LineBreaks;
            if (line > lineBreaks.Count)
            {
                return null;
            }

            // Find the bound node corresponding to the text position
            int lineStart = (line == 0) ? 0 : lineBreaks.EndOfLineBreak(line - 1);
            int position = lineStart + character;
            var searchVisitor = new PositionSearchVisitor(position);
            SourceRoutineSymbol resultRoutine = null;
            foreach (var routine in boundFile.AllRoutines)
            {
                // Consider only routines containing the position being searched (<Main> has invalid span)
                var routineSpan = routine.Syntax.BodySpanOrInvalid();
                if (!routineSpan.IsValid || routineSpan.Contains(position))
                {
                    searchVisitor.VisitCFG(routine.ControlFlowGraph);
                    if (searchVisitor.Result != null)
                    {
                        resultRoutine = routine;
                        break;
                    }
                }
            }

            if (searchVisitor.Result == null)
            {
                return null;
            }

            return FormulateHoverHint(resultRoutine, searchVisitor.Result);
        }

        private string FormulateHoverHint(SourceRoutineSymbol routine, IPhpOperation operation)
        {
            if (operation is BoundVariableRef varRef && varRef.Name.IsDirect)
            {
                var text = new StringBuilder();

                switch (varRef.Variable.VariableKind)
                {
                    case VariableKind.LocalVariable:
                        text.Append("(local) ");
                        break;
                    case VariableKind.GlobalVariable:
                        text.Append("global ");
                        break;
                    case VariableKind.Parameter:
                        text.Append("(parameter) ");
                        break;
                    case VariableKind.ThisParameter:
                        break;
                    case VariableKind.StaticVariable:
                        text.Append("(static) ");
                        break;
                    default:
                        break;
                }

                text.Append($"${varRef.Name.NameValue.Value} : ");

                if (varRef.TypeRefMask.IsAnyType)
                {
                    text.Append("mixed");
                }
                else
                {
                    // Display types ordered alphabetically, duplicate types (such as PHP and .NET "string") are unified
                    var types = routine.TypeRefContext.GetTypes(varRef.TypeRefMask);
                    var typeNames = types
                        .Select(t => t.QualifiedName.ToString())
                        .ToImmutableSortedSet();
                    text.Append(string.Join(" | ", typeNames)); 
                }

                return text.ToString();
            }
            else
            {
                return null;
            }
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

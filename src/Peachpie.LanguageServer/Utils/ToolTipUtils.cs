using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Peachpie.LanguageServer
{
    internal static class ToolTipUtils
    {
        /// <summary>
        /// Returns the text of a tooltip corresponding to the given position in the code.
        /// </summary>
        public static string ObtainToolTip(PhpCompilation compilation, string filepath, int line, int character)
        {
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
        /// Creates the text of a tooltip.
        /// </summary>
        private static string FormulateToolTip(SourceSymbolSearcher.SymbolStat searchResult)
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
    }
}

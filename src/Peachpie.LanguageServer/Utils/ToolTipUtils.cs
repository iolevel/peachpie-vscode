﻿using Devsense.PHP.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using Pchp.CodeAnalysis;
using Pchp.CodeAnalysis.FlowAnalysis;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Symbols;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Peachpie.LanguageServer
{
    internal static class ToolTipUtils
    {
        public static SourceSymbolSearcher.SymbolStat FindDefinition(PhpCompilation compilation, string filepath, int line, int character)
        {
            var tree = compilation.SyntaxTrees.FirstOrDefault(t => t.FilePath == filepath);
            if (tree == null)
            {
                return null;
            }

            // Get the position in the file
            int position = tree.GetOffset(new LinePosition(line, character));
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
                    searchResult = SourceSymbolSearcher.SearchCFG(compilation, routine.ControlFlowGraph, position);
                    if (searchResult != null)
                    {
                        break;
                    }
                }
            }

            return searchResult;
        }

        public static IEnumerable<Protocol.Location> ObtainDefinition(PhpCompilation compilation, string filepath, int line, int character)
        {
            var result = FindDefinition(compilation, filepath, line, character);
            if (result != null && result.Symbol != null)
            {
                foreach (var loc in result.Symbol.Locations)
                {
                    var pos = loc.GetLineSpan();
                    if (pos.IsValid)
                    {
                        yield return new Protocol.Location
                        {
                            Uri = new Uri(pos.Path).AbsoluteUri,
                            Range = pos.AsRange(),
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Returns the text of a tooltip corresponding to the given position in the code.
        /// </summary>
        public static ToolTipInfo ObtainToolTip(PhpCompilation compilation, string filepath, int line, int character)
        {
            return FormulateToolTip(FindDefinition(compilation, filepath, line, character));
        }

        static string FormulateTypeHint(ITypeSymbol type)
        {
            if (type == null)
            {
                return null;
            }

            if (type.MetadataName == "PhpValue" || type.MetadataName == "PhpAlias") // TODO: helpers!!!
            {
                return null;    // "mixed", "&"
            }

            return type.Name;
        }

        /// <summary>
        /// Creates the text of a tooltip.
        /// </summary>
        private static ToolTipInfo FormulateToolTip(SourceSymbolSearcher.SymbolStat searchResult)
        {
            if (searchResult == null || (searchResult.Symbol == null && searchResult.BoundExpression == null))
            {
                return null;
            }

            var ctx = searchResult.TypeCtx;
            var expression = searchResult.BoundExpression;
            var symbol = searchResult.Symbol;
            if (symbol is IErrorMethodSymbol errSymbol)
            {
                if (errSymbol.ErrorKind == ErrorMethodKind.Missing)
                {
                    return null;
                }

                symbol = errSymbol.OriginalSymbols.LastOrDefault();
            }

            var result = new StringBuilder(32);
            var kind = string.Empty;

            if (expression is BoundVariableRef varref && varref.Name.IsDirect)
            {
                var name = varref.Name.NameValue;

                if (symbol is IParameterSymbol)
                {
                    kind = "parameter";
                }
                else
                {
                    kind = "variable";
                }

                //switch ((((BoundVariableRef)expression).Variable).VariableKind)
                //{
                //    case VariableKind.LocalVariable:
                //        result.Append("(var) "); break;
                //    case VariableKind.Parameter:
                //        result.Append("(parameter) "); break;
                //    case VariableKind.GlobalVariable:
                //        result.Append("(global) "); break;
                //    case VariableKind.StaticVariable:
                //        result.Append("(static local) "); break;
                //}

                result.Append("$" + name);

            }
            else if (expression is BoundGlobalConst)
            {
                kind = "global constant";
                result.Append(((BoundGlobalConst)expression).Name);
            }
            else if (expression is BoundPseudoConst)
            {
                kind = "magic constant";
                result.Append("__");
                result.Append(((BoundPseudoConst)expression).ConstType.ToString().ToUpperInvariant());
                result.Append("__");
            }
            else if (symbol is IParameterSymbol)
            {
                kind = "parameter";
                result.Append("$" + symbol.Name);
            }
            else if (symbol is IPhpRoutineSymbol)
            {
                var routine = (IPhpRoutineSymbol)symbol;
                result.Append("function ");
                result.Append(routine.RoutineName);
                result.Append('(');
                int nopt = 0;
                bool first = true;
                foreach (var p in routine.Parameters)
                {
                    if (p.IsImplicitlyDeclared) continue;
                    if (p.IsOptional)
                    {
                        nopt++;
                        result.Append('[');
                    }
                    if (first) first = false;
                    else result.Append(", ");

                    var thint = FormulateTypeHint(p.Type);
                    if (thint != null)
                    {
                        result.Append(thint + " ");
                    }
                    result.Append("$" + p.Name);
                }
                while (nopt-- > 0) { result.Append(']'); }
                result.Append(')');
            }
            else if (expression is BoundFieldRef fld)
            {
                if (fld.FieldName.IsDirect)
                {
                    string containedType = null;

                    if (fld.IsClassConstant)
                    {
                        kind = "class constant";
                    }
                    else
                    {
                        result.Append(fld.IsStaticField ? "static" : "var");
                    };

                    if (fld.Instance != null)
                    {
                        //if (fld.Instance.TypeRefMask.IsAnyType || fld.Instance.TypeRefMask.IsVoid) return null;

                        containedType = ctx.ToString(fld.Instance.TypeRefMask);
                    }
                    else
                    {
                        containedType = fld.ContainingType?.ToString();
                    }

                    result.Append(' ');
                    if (containedType != null)
                    {
                        result.Append(containedType);
                        result.Append(Name.ClassMemberSeparator);
                    }
                    if (!fld.IsClassConstant)
                    {
                        result.Append("$");
                    }
                    result.Append(fld.FieldName.NameValue.Value);
                }
                else
                {
                    return null;
                }
            }
            else if (symbol is IPhpTypeSymbol phpt)
            {
                kind = "type";
                if (phpt.TypeKind == TypeKind.Interface) result.Append("interface");
                else if (phpt.IsTrait) result.Append("trait");
                else result.Append("class");

                result.Append(' ');
                result.Append(phpt.FullName.ToString());
            }
            else
            {
                return null;
            }

            // : type
            if (ctx != null)
            {
                TypeRefMask? mask = null;

                if (expression != null)
                {
                    mask = expression.TypeRefMask;
                }
                else if (symbol is IPhpValue resultVal)
                {
                    mask = resultVal.GetResultType(ctx);
                }

                if (mask != null)
                {
                    result.Append(": ");
                    result.Append(ctx.ToString(mask.Value));
                }
            }

            // constant value
            if (expression != null && expression.ConstantValue.HasValue)
            {
                //  = <value>
                var value = expression.ConstantValue.Value;
                string valueStr;
                if (value == null) valueStr = "NULL";
                else if (value is int) valueStr = ((int)value).ToString();
                else if (value is string) valueStr = "\"" + ((string)value).Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
                else if (value is long) valueStr = ((long)value).ToString();
                else if (value is double) valueStr = ((double)value).ToString(CultureInfo.InvariantCulture);
                else if (value is bool) valueStr = (bool)value ? "TRUE" : "FALSE";
                else valueStr = value.ToString();

                if (valueStr != null)
                {
                    result.Append(" = ");
                    result.Append(valueStr);
                }
            }

            string description;

            try
            {
                description = XmlDocumentationToMarkdown(symbol?.GetDocumentationCommentXml());
            }
            catch
            {
                description = null;
            }

            // description
            return new ToolTipInfo("<?php //" + kind + "\n" + result.ToString(), description);
        }

        static string XmlDocumentationToMarkdown(string xmldoc)
        {
            if (string.IsNullOrWhiteSpace(xmldoc))
            {
                return string.Empty;
            }

            var result = new StringBuilder(xmldoc.Length);
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                IgnoreWhitespace = true,
                ValidationType = ValidationType.None
            };

            using (var xml = XmlReader.Create(new StringReader("<summary>" + xmldoc + "</summary>"), settings))
            {
                bool skipped = false;
                while (skipped || xml.Read())
                {
                    skipped = false;

                    switch (xml.NodeType)
                    {
                        case XmlNodeType.Element:
                            // <summary>
                            switch (xml.Name.ToLowerInvariant())
                            {
                                case "summary":
                                    break;  // OK
                                case "remarks":
                                    result.Append("\n\n**Remarks:**\n");
                                    break;
                                case "returns":
                                    result.Append("\n\n**Returns:**\n");
                                    break;
                                case "c":
                                    result.AppendFormat("**{0}**", xml.ReadInnerXml());
                                    break;
                                case "paramref":
                                    if (xml.HasAttributes)
                                        result.AppendFormat("**${0}**", xml.GetAttribute("name"));
                                    break;
                                case "code":
                                    result.AppendFormat("```\n{0}```\n", xml.ReadInnerXml());
                                    break;
                                case "see":
                                    if (xml.HasAttributes)
                                        result.AppendFormat("**{0}**", xml.GetAttribute("cref"));
                                    break;
                                case "a":
                                    if (xml.HasAttributes)
                                        result.AppendFormat("({1})[{0}]", xml.GetAttribute("href"), xml.ReadInnerXml());
                                    break;
                                default:
                                    xml.Skip();
                                    skipped = true; // do not call Read()!
                                    break;
                            }
                            break;

                        case XmlNodeType.Text:
                            result.Append(xml.Value.Replace("*", "\\*"));
                            break;
                    }
                }
            }

            //
            return result.ToString().Trim();
        }
    }
}

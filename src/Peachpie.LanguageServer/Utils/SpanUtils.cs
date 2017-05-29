using Devsense.PHP.Text;
using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.Symbols;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Peachpie.LanguageServer
{
    static class SpanUtils
    {
        public static Span ToSpan(this Microsoft.CodeAnalysis.Text.TextSpan span)
        {
            return new Span(span.Start, span.Length);
        }

        public static Microsoft.CodeAnalysis.Text.TextSpan ToSpan(this Span span)
        {
            return new Microsoft.CodeAnalysis.Text.TextSpan(span.Start, span.Length);
        }

        public static Span GetSpan(this IPhpRoutineSymbol routine)
        {
            return routine.Locations.FirstOrDefault(l => l.IsInSource)?.SourceSpan.ToSpan() ?? Span.Invalid;
        }

        public static Span GetSpan(this IParameterSymbol parameter)
        {
            return parameter.IsImplicitlyDeclared ? Span.Invalid : 
                parameter.Locations.FirstOrDefault(l => l.IsInSource)?.SourceSpan.ToSpan() ?? Span.Invalid;
        }
    }
}

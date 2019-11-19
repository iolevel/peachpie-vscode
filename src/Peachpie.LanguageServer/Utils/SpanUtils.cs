using Devsense.PHP.Text;
using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.Symbols;
using Peachpie.LanguageServer.Protocol;
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

        public static Protocol.Range AsRange(this Microsoft.CodeAnalysis.Location location) => AsRange(location.GetLineSpan());

        public static Protocol.Range AsRange(this FileLinePositionSpan span)
        {
            return new Range(
                new Position(span.StartLinePosition.Line, span.StartLinePosition.Character),
                new Position(span.EndLinePosition.Line, span.EndLinePosition.Character));
        }
    }
}

using Devsense.PHP.Text;
using System;
using System.Collections.Generic;
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
    }
}

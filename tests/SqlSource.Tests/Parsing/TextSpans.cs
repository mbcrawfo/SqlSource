using System;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Tests.Parsing;

// The span of a value in a text, for the spans the parser's tests expect.
internal static class TextSpans
{
    // The first occurrence of the value, or the last when last is set.
    public static TextSpan Of(string text, string value, bool last = false) =>
        new(
            last ? text.LastIndexOf(value, StringComparison.Ordinal) : text.IndexOf(value, StringComparison.Ordinal),
            value.Length
        );
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A problem found in a <c>.sql</c> file.
/// </summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the file's text.</param>
/// <param name="Arguments">The text the message quotes, such as the offending name.  Empty for most kinds.</param>
internal sealed record SqlParseError(SqlParseErrorKind Kind, TextSpan Span, EquatableArray<string> Arguments)
{
    public static SqlParseError Create(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
        new(kind, span, new EquatableArray<string>(ImmutableArray.Create(arguments)));
}

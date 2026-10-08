using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// A problem found in a <c>.sql</c> file.
/// </summary>
/// <param name="Kind">What is wrong.</param>
/// <param name="Span">Where it is, as offsets into the file's text.</param>
/// <param name="Arguments">
/// The text a message quotes.  <see cref="SqlParseErrorKind" /> says what each kind carries: one argument, two, or
/// none.
/// </param>
internal sealed record SqlParseError(SqlParseErrorKind Kind, TextSpan Span, EquatableArray<string> Arguments)
{
    public static SqlParseError Create(SqlParseErrorKind kind, TextSpan span, params string[] arguments) =>
        new(kind, span, new EquatableArray<string>(ImmutableArray.Create(arguments)));
}

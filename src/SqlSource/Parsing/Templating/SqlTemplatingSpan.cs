using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing.Templating;

/// <summary>
/// One span of a file that the sqlfluff templater replaces or protects.
/// </summary>
/// <param name="Kind">What the span is.</param>
/// <param name="Span">Where it is, as offsets into the file's text.  Empty for a query's end.</param>
/// <param name="Name">The token's or the parameter's name, without its prefix, or null.</param>
/// <param name="Default">The default the token's query resolves for it, trimmed, or null.</param>
internal readonly record struct SqlTemplatingSpan(
    SqlTemplatingSpanKind Kind,
    TextSpan Span,
    string? Name,
    string? Default
);

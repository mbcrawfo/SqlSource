using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// One place a token is written in a block's SQL.
/// </summary>
/// <param name="Name">The token's name.</param>
/// <param name="Default">
/// The default written there, trimmed.  Empty for <c>{{name:}}</c>, null for <c>{{name}}</c>.
/// </param>
/// <param name="Span">The whole token, braces included, as offsets into the scanned SQL.</param>
internal readonly record struct SqlTokenOccurrence(string Name, string? Default, TextSpan Span);

using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// One named query from a <c>.sql</c> file.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameSpan">
/// Where the name is in the file.  An empty span at the start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the block's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="Shape">
/// The shape the name marker gives, or null when it gives none.  It has no effect yet.
/// </param>
/// <param name="Segments">The SQL without comments, split into literal text and tokens.  Never empty.</param>
/// <param name="KeptSegments">
/// The SQL with its comments and blank lines, or null when that form was not built: because nothing can ask for it,
/// or because it is the same text as <paramref name="Segments" />.
/// </param>
/// <param name="Tokens">
/// The query's tokens, each once, in order of first appearance, with its default.  Empty when the SQL has none.
/// </param>
/// <param name="Parameters">The query's parameters, in order of first appearance.  Empty when it has none.</param>
/// <param name="Markers">What the query's markers say about the settings, over those of its file's preamble.</param>
/// <param name="InputModelName">
/// The full name or the name a <c>-- input-model:</c> marker gives the type of the query's parameters, or null.
/// </param>
/// <param name="OutputModelName">
/// The full name or the name an <c>-- output-model:</c> marker gives the type of the query's rows, or null.
/// </param>
internal sealed record SqlBlock(
    string Name,
    TextSpan NameSpan,
    string? Summary,
    ResultShape? Shape,
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlSegment>? KeptSegments,
    EquatableArray<SqlToken> Tokens,
    EquatableArray<SqlQueryParameter> Parameters,
    SettingsLevel Markers,
    string? InputModelName,
    string? OutputModelName
);

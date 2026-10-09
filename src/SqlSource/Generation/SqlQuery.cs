using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// One query of a <c>.sql</c> file.  It becomes a constant, or a method when its SQL has a token.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameLocation">
/// Where the name is in the file.  The start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="Shape">
/// The shape the name marker gives, or null when it gives none.  It has no effect yet.
/// </param>
/// <param name="Segments">
/// The SQL without comments, split into literal text and tokens.  Never empty.  Without a token it is one literal
/// segment.
/// </param>
/// <param name="KeptSegments">
/// The SQL with its comments and blank lines, or null when that form was not built: because nothing can ask for it,
/// or because it is the same text as <paramref name="Segments" />.
/// </param>
/// <param name="Tokens">
/// The query's tokens, each once, in order of first appearance, with its default.  Empty when the SQL has none.
/// </param>
/// <param name="Parameters">The query's parameters, in order of first appearance.  Empty when it has none.</param>
/// <param name="Markers">What the query's markers say about the settings, over those of its file's preamble.</param>
internal sealed record SqlQuery(
    string Name,
    LocationInfo NameLocation,
    string? Summary,
    ResultShape? Shape,
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlSegment>? KeptSegments,
    EquatableArray<SqlToken> Tokens,
    EquatableArray<SqlQueryParameter> Parameters,
    SettingsLevel Markers
);

using Microsoft.CodeAnalysis.Text;
using SqlSource.Parsing.Tokens;
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
/// <param name="Parameters">
/// The query's parameters: those of its SQL in order of first appearance, then those that only a <c>-- param:</c>
/// marker declares, in marker order.  Empty when it has none.
/// </param>
/// <param name="Markers">What the query's markers say about the settings, over those of its file's preamble.</param>
/// <param name="InputModel">
/// The name a <c>-- input-model:</c> marker gives the type of the query's parameters, with the marker in effect and
/// where it is, or null.
/// </param>
/// <param name="OutputModel">
/// The name an <c>-- output-model:</c> marker gives the type of the query's rows, with the marker in effect and where
/// it is, or null.
/// </param>
/// <param name="ColumnMarkers">
/// What the query's <c>-- column:</c> markers say of the nullability of its columns, in marker order, each column
/// once.  Empty when it has none.
/// </param>
/// <param name="KeptErrors">
/// The errors of the scan of <paramref name="KeptSegments" /> for tokens, as offsets into the file's text.  They are
/// errors only for a type that emits that form, which reports them.  An error of the form without comments is the
/// file's, so these are of the comments alone.  Empty when there are none or the form was not built.
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
    SqlModelName? InputModel,
    SqlModelName? OutputModel,
    EquatableArray<SqlColumnMarker> ColumnMarkers,
    EquatableArray<SqlParseError> KeptErrors
);

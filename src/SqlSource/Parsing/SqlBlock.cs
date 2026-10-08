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
/// <param name="KeepComments">Whether comments were kept in the SQL.</param>
/// <param name="TokenValidation">
/// True or false when a validation generator parameter applies to the block, null when none does.
/// </param>
/// <param name="Segments">The SQL, split into literal text and tokens.  Never empty.</param>
/// <param name="Tokens">
/// The query's tokens, each once, in order of first appearance, with its default.  Empty when the SQL has none.
/// </param>
/// <param name="Parameters">The query's parameters, in order of first appearance.  Empty when it has none.</param>
internal sealed record SqlBlock(
    string Name,
    TextSpan NameSpan,
    string? Summary,
    ResultShape? Shape,
    bool KeepComments,
    bool? TokenValidation,
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlToken> Tokens,
    EquatableArray<SqlQueryParameter> Parameters
);

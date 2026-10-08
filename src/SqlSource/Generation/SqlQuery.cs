using SqlSource.Diagnostics;
using SqlSource.Parsing;

namespace SqlSource.Generation;

/// <summary>
/// One query of a <c>.sql</c> file.  It becomes a constant, or a method when its SQL has a token.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameLocation">
/// Where the name is in the file.  The start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="Segments">
/// The SQL, split into literal text and tokens.  Never empty.  Without a token it is one literal segment.
/// </param>
/// <param name="Tokens">
/// The query's tokens, each once, in order of first appearance, with its default.  Empty when the SQL has none.
/// </param>
/// <param name="TokenValidation">
/// True or false when a validation generator parameter applies to the query, null when the project's setting decides.
/// </param>
/// <param name="Parameters">The query's parameters, in order of first appearance.  Empty when it has none.</param>
internal sealed record SqlQuery(
    string Name,
    LocationInfo NameLocation,
    string? Summary,
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlToken> Tokens,
    bool? TokenValidation,
    EquatableArray<SqlQueryParameter> Parameters
);

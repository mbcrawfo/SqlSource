using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Parsing.Tokens;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// One query of a <c>.sql</c> file.  It becomes a constant, or a method when its SQL has a token, and its models when
/// its output asks for them; when its output is <c>Methods</c>, the method that runs it takes the place of the
/// constant.
/// </summary>
/// <param name="Name">The query's name, a valid C# identifier.</param>
/// <param name="NameLocation">
/// Where the name is in the file.  The start of the file when the name comes from the file name.
/// </param>
/// <param name="Summary">The text of the query's <c>-- summary:</c> markers, or null when it has none.</param>
/// <param name="Shape">
/// The shape the name marker gives, or null when it gives none, which the query's method returns.
/// </param>
/// <param name="Segments">
/// The SQL without comments, split into literal text, tokens and the uses of its lists.  Never empty.  Without a token
/// or a list it is one literal segment.
/// </param>
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
/// What the query's <c>-- column:</c> markers say of its columns, their nullability and C# type, in marker order, each
/// column once.  Empty when it has none.
/// </param>
/// <param name="KeptErrors">
/// The errors of the scan of <paramref name="KeptSegments" /> for tokens, of the comments alone.  They are errors only
/// for a type that keeps the query's comments, which reports them and does not use the file.  Empty when there are
/// none.
/// </param>
internal sealed record SqlQuery(
    string Name,
    LocationInfo NameLocation,
    string? Summary,
    ResultShape? Shape,
    EquatableArray<SqlSegment> Segments,
    EquatableArray<SqlSegment>? KeptSegments,
    EquatableArray<SqlToken> Tokens,
    EquatableArray<SqlQueryParameter> Parameters,
    SettingsLevel Markers,
    ModelMarker? InputModel,
    ModelMarker? OutputModel,
    EquatableArray<ColumnMarker> ColumnMarkers,
    EquatableArray<DiagnosticInfo> KeptErrors
)
{
    /// <summary>
    /// Where the query's own <c>-- member-location:</c> marker is, never its header's, or null.  A query whose file is
    /// one query has the marker of the whole file.
    /// </summary>
    public LocationInfo? MemberLocationMarker { get; init; }

    /// <summary>
    /// Where the query's own <c>-- visibility:</c> marker is, never its header's, or null.  A query whose file is one
    /// query has the marker of the whole file.
    /// </summary>
    public LocationInfo? VisibilityMarker { get; init; }

    /// <summary>
    /// The names the query's SQL declares as local variables, as written, without their prefix, which are none of its
    /// <see cref="Parameters" /> and which no parameter its method sends may take.  Empty when it declares none.
    /// </summary>
    public EquatableArray<string> DeclaredVariables { get; init; }

    /// <summary>
    /// The errors of the query's SQL that are errors only when its output is <see cref="OutputKind.Methods" />: a list
    /// that its method would send in a form the database refuses.  <c>MethodPlanner</c> reports them and gives the
    /// query no method.  Empty when there are none.
    /// </summary>
    public EquatableArray<DiagnosticInfo> MethodErrors { get; init; }
}

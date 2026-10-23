using SqlSource.Snapshot;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// The members both describers' events share, each built in one place, so that an engine's event names a table, an
/// origin or a verdict as the other engine's does and a log is read by one name for both.
/// </summary>
/// <remarks>
/// A member is added and never renamed: what an engine's event writes beside these is its own.
/// </remarks>
internal static class DescriberEvents
{
    /// <summary>
    /// The members that name a relation in every log: its <c>schema</c> and its <c>table</c> apart, as a name may hold
    /// a dot.  A map or an event that has more members of its own writes these first.
    /// </summary>
    /// <param name="schema">The relation's schema, or null.</param>
    /// <param name="table">The relation's name, or null.</param>
    public static LogMember[] TableMembers(string? schema, string? table) =>
        [new("schema", schema), new("table", table)];

    /// <summary>A relation as every log writes it: a map of <see cref="TableMembers" />.</summary>
    /// <param name="schema">The relation's schema, or null.</param>
    /// <param name="table">The relation's name, or null.</param>
    public static LogValue TableOf(string? schema, string? table) => LogValue.MapOf(TableMembers(schema, table));

    /// <summary>
    /// The origin of a column: its <c>schema</c>, <c>table</c> and <c>column</c>, or <see cref="LogValue.Null" /> for a
    /// column of none.
    /// </summary>
    /// <param name="origin">The column's origin, or null.</param>
    public static LogValue OriginOf(SidecarOrigin? origin) =>
        origin is null
            ? LogValue.Null
            : LogValue.MapOf(new("schema", origin.Schema), new("table", origin.Table), new("column", origin.Column));

    /// <summary>
    /// The members both <c>column</c> events start with: <c>query</c>, <c>ordinal</c>, <c>name</c>, <c>type</c> and
    /// <c>origin</c>.
    /// </summary>
    /// <param name="query">The query's name.</param>
    /// <param name="column">The column.</param>
    public static LogMember[] ColumnHead(string query, SidecarColumn column) =>
        [
            new("query", query),
            new("ordinal", column.Ordinal),
            new("name", column.Name),
            new("type", column.Type.Name),
            new("origin", OriginOf(column.Origin)),
        ];

    /// <summary>
    /// The members both <c>table-match</c> events end with: <c>tableMatch</c>, the verdict, and <c>table</c>, the
    /// relation whose column list the result is or <see cref="LogValue.Null" />.
    /// </summary>
    /// <param name="table">The matched table, or null when the match failed.</param>
    /// <param name="tableMatch">The verdict: <c>matched</c>, or the first check of the match that failed.</param>
    public static LogMember[] TableMatchTail(SidecarTable? table, string tableMatch) =>
        [
            new("tableMatch", tableMatch),
            new("table", table is null ? LogValue.Null : TableOf(table.Schema, table.Table)),
        ];
}

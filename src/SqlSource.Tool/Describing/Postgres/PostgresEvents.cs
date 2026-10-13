using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The events of the PostgreSQL describer, each built in one place: what a call asked and what the server answered,
/// as the capture holds it.
/// </summary>
/// <remarks>
/// The names are written without the engine: the log a describer is given puts <c>postgres.</c> before each, and
/// removes the connection's value from every string.  A member is added and never renamed.  <c>open</c> has no
/// event of its own: the run's <c>database.open</c> records what the session knows of its server.  A describe
/// writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, one <c>column</c> for each column,
/// <c>table-match</c>, <c>check</c> and <c>deallocate</c>, in that order, each only when its call was made, and
/// <c>table-match</c> only for a statement with rows whose columns were all built.
/// </remarks>
internal static class PostgresEvents
{
    private const string Error = "error";
    private const string Duration = "durationMs";

    /// <summary>
    /// <c>postgres.prepare</c>: the SQL as it was sent, and the types the server resolved or its error.
    /// </summary>
    public static void Prepare(
        IRunLog log,
        string query,
        PostgresPrepareRequest request,
        PostgresPrepareAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "prepare",
            new("query", query),
            new("statement", request.Statement),
            new("sql", request.Sql),
            new("types", LogValue.ListOfTexts(request.Types)),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember(
                    "parameterTypes",
                    LogValue.ListOf((answer.ParameterTypes ?? []).Select(static oid => (LogValue)oid))
                ),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.types</c>: each row as the answer holds it, or the error.</summary>
    public static void Types(IRunLog log, PostgresTypesRequest request, PostgresTypesAnswer answer, long durationMs) =>
        log.Write(
            "types",
            new("oids", LogValue.ListOf(request.Oids.Select(static oid => (LogValue)oid))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("rows", LogValue.ListOf((answer.Rows ?? []).Select(RowOf))),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.columns</c>: each column as the answer holds it, or the error.</summary>
    public static void Columns(
        IRunLog log,
        string query,
        PostgresColumnsRequest request,
        PostgresColumnsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "columns",
            new("query", query),
            new("statement", request.Statement),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("columns", LogValue.ListOf((answer.Columns ?? []).Select(ColumnOf))),
            new(Duration, durationMs)
        );

    /// <summary><c>postgres.tables</c>: each table with its columns as the answer holds them, or the error.</summary>
    public static void Tables(
        IRunLog log,
        PostgresTablesRequest request,
        PostgresTablesAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "tables",
            new("oids", LogValue.ListOf(request.Oids.Select(static oid => (LogValue)oid))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("tables", LogValue.ListOf((answer.Tables ?? []).Select(TableOf))),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.check</c>: the name and the type name of each parameter, whether the two describes agree, and
    /// what differs.  The SQL is in <c>postgres.prepare</c>.
    /// </summary>
    public static void Check(
        IRunLog log,
        string query,
        PostgresCheckRequest request,
        CheckVerdict verdict,
        long durationMs
    ) =>
        log.Write(
            "check",
            new("query", query),
            new(
                "parameters",
                LogValue.ListOf(
                    request.Parameters.Select(static parameter =>
                        LogValue.MapOf(new("name", parameter.Name), new("dataTypeName", parameter.DataTypeName))
                    )
                )
            ),
            new("result", verdict.Result),
            new("differs", verdict.Differs),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.column</c>: one column as it will be recorded, and whether its query was gated.  It is the one
    /// place that says why a column is nullable beside what was asked to find out.
    /// </summary>
    public static void Column(IRunLog log, string query, SidecarColumn column, bool gated) =>
        log.Write(
            "column",
            new("query", query),
            new("ordinal", column.Ordinal),
            new("name", column.Name),
            new("type", column.Type.Name),
            new(
                "origin",
                column.Origin is { } origin
                    ? LogValue.MapOf(
                        new("schema", origin.Schema),
                        new("table", origin.Table),
                        new("column", origin.Column)
                    )
                    : LogValue.Null
            ),
            new("nullable", column.Nullable is { } nullable ? (LogValue)nullable : LogValue.Null),
            new("nullableSource", column.NullableSource),
            new("gated", gated)
        );

    /// <summary>
    /// <c>postgres.table-match</c>: <c>matched</c> with the relation whose column list the result is, as
    /// <c>schema.name</c>, or the first check of the match that failed with no table.
    /// </summary>
    public static void TableMatch(IRunLog log, string query, SidecarTable? table, string result) =>
        log.Write(
            "table-match",
            new("query", query),
            new("result", result),
            new("table", table is null ? LogValue.Null : (LogValue)$"{table.Schema}.{table.Table}")
        );

    /// <summary><c>postgres.deallocate</c>: the one place that says a statement could not be freed.</summary>
    public static void Deallocate(
        IRunLog log,
        PostgresDeallocateRequest request,
        PostgresDeallocateAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "deallocate",
            new("statement", request.Statement),
            new(Error, answer.Error is { } error ? ErrorOf(error) : LogValue.Null),
            new(Duration, durationMs)
        );

    private static LogValue ErrorOf(PostgresServerError error) =>
        LogValue.MapOf(
            new("sqlState", error.SqlState),
            new("message", error.Message),
            new("detail", error.Detail),
            new("hint", error.Hint),
            new("position", LogValue.Of(error.Position))
        );

    private static LogValue ColumnOf(PostgresColumnRow column) =>
        LogValue.MapOf(
            new("ordinal", column.Ordinal),
            new("name", column.Name),
            new("typeOid", column.TypeOid),
            new("tableOid", column.TableOid),
            new("attributeNumber", column.AttributeNumber),
            new("length", LogValue.Of(column.Length)),
            new("precision", LogValue.Of(column.Precision)),
            new("scale", LogValue.Of(column.Scale))
        );

    private static LogValue TableOf(PostgresTableRow table) =>
        LogValue.MapOf(
            new("oid", table.Oid),
            new("relkind", table.Relkind),
            new("schema", table.Schema),
            new("name", table.Name),
            new("columns", LogValue.ListOf(table.Columns.Select(AttributeOf)))
        );

    private static LogValue AttributeOf(PostgresAttributeRow attribute) =>
        LogValue.MapOf(
            new("attnum", attribute.Attnum),
            new("name", attribute.Name),
            new("notNull", attribute.NotNull),
            new("identity", attribute.Identity),
            new("generated", attribute.Generated),
            new("typeOid", attribute.TypeOid),
            new("notNullValidated", attribute.NotNullValidated is { } validated ? (LogValue)validated : LogValue.Null)
        );

    private static LogValue RowOf(PostgresTypeRow row) =>
        LogValue.MapOf(
            new("oid", row.Oid),
            new("typname", row.Typname),
            new("nspname", row.Nspname),
            new("typtype", row.Typtype),
            new("typcategory", row.Typcategory),
            new("typelem", row.Typelem),
            new("typbasetype", row.Typbasetype),
            new("rngsubtype", LogValue.Of(row.Rngsubtype)),
            new("multirangeRngsubtype", LogValue.Of(row.MultirangeRngsubtype)),
            new("formatType", row.FormatType),
            new("enumLabels", row.EnumLabels is { } labels ? LogValue.ListOfTexts(labels) : LogValue.Null)
        );
}

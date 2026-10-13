using System.Linq;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The events of the PostgreSQL describer, each built in one place: what a call asked and what the server answered,
/// as the capture holds it.
/// </summary>
/// <remarks>
/// The names are written without the engine: the log a describer is given puts <c>postgres.</c> before each, and
/// removes the connection's value from every string.  A member is added and never renamed.  <c>open</c> has no
/// event of its own: the run's <c>database.open</c> records what the session knows of its server.
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

using System.Linq;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// The events of the SQL Server describer, each built in one place: what a call asked and what the server answered,
/// as the capture holds it.
/// </summary>
/// <remarks>
/// The names are written without the engine: the log a describer is given puts <c>mssql.</c> before each, and removes
/// the connection's value from every string.  A member is added and never renamed.  Every event has the name of the
/// query as its first member, <c>query</c>, then the SQL the call sent as <c>sql</c> and the declarations it sent as
/// <c>params</c>, and last <c>durationMs</c>.  A row is a map of every member of the record, named as the capture
/// names it.  An error is the list of every error of the exception, in its order, each a map of <c>number</c>,
/// <c>class</c>, <c>state</c> and <c>message</c>; the describer's own text of a connection that was lost or timed out
/// has 0 for each number.  <c>open</c> has no event of its own: the run's <c>database.open</c> records what the
/// session knows of its server, the four settings included.  A describe writes <c>declared</c> for each declared
/// parameter, in the list's order, then <c>parameters</c>, then <c>probe</c> for each probe, one or two for each copy
/// of a reused parameter whose copies' types differ, each only when its call was made.
/// </remarks>
internal static class SqlServerEvents
{
    private const string Error = "error";
    private const string Duration = "durationMs";

    /// <summary>
    /// <c>mssql.parameters</c>: the SQL with each reused parameter renamed to its copies and the declarations, and
    /// each parameter the server typed, or the error.
    /// </summary>
    public static void Parameters(
        IRunLog log,
        string query,
        SqlServerParametersRequest request,
        SqlServerParametersAnswer answer,
        long durationMs
    ) =>
        log.Write(
            SqlServerCallNames.Parameters,
            new("query", query),
            new("sql", request.Sql),
            new("params", request.Params),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("rows", LogValue.ListOf((answer.Rows ?? []).Select(ParameterOf))),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>mssql.probe</c>: the SQL and the declarations of one probe of a copy, and whether the SQL binds with them,
    /// or the error of a failure that is not the batch's.
    /// </summary>
    public static void Probe(
        IRunLog log,
        string query,
        SqlServerProbeRequest request,
        SqlServerProbeAnswer answer,
        long durationMs
    ) =>
        log.Write(
            SqlServerCallNames.Probe,
            new("query", query),
            new("sql", request.Sql),
            new("params", request.Params),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("binds", LogValue.Of(answer.Binds)),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>mssql.declared</c>: the <c>SELECT</c> of a declared parameter and its declaration, and the column the
    /// server described, the error rows of the describe, or the error of the call.
    /// </summary>
    public static void Declared(
        IRunLog log,
        string query,
        SqlServerDeclaredRequest request,
        SqlServerDeclaredAnswer answer,
        long durationMs
    ) =>
        log.Write(
            SqlServerCallNames.Declared,
            new("query", query),
            new("sql", request.Sql),
            new("params", request.Params),
            answer switch
            {
                { Error: { } error } => new LogMember(Error, ErrorOf(error)),
                { Errors: { Count: > 0 } errors } => new LogMember(
                    "errors",
                    LogValue.ListOf(errors.Select(DescribeErrorOf))
                ),
                _ => new LogMember("rows", LogValue.ListOf((answer.Rows ?? []).Select(DeclaredRowOf))),
            },
            new(Duration, durationMs)
        );

    private static LogValue ErrorOf(SqlServerServerError error) =>
        LogValue.ListOf(
            error.Errors.Select(static row =>
                LogValue.MapOf(
                    new("number", row.Number),
                    new("class", row.Class),
                    new("state", row.State),
                    new("message", row.Message)
                )
            )
        );

    private static LogValue ParameterOf(SqlServerParameterRow row) =>
        LogValue.MapOf(
            new("name", row.Name),
            new("suggestedSystemTypeName", row.SuggestedSystemTypeName),
            new("suggestedMaxLength", LogValue.Of(row.SuggestedMaxLength)),
            new("suggestedPrecision", LogValue.Of(row.SuggestedPrecision)),
            new("suggestedScale", LogValue.Of(row.SuggestedScale)),
            new("suggestedUserTypeSchema", row.SuggestedUserTypeSchema),
            new("suggestedUserTypeName", row.SuggestedUserTypeName),
            new("suggestedAssemblyQualifiedTypeName", row.SuggestedAssemblyQualifiedTypeName)
        );

    private static LogValue DeclaredRowOf(SqlServerDeclaredRow row) =>
        LogValue.MapOf(
            new("columnOrdinal", row.ColumnOrdinal),
            new("systemTypeName", row.SystemTypeName),
            new("maxLength", LogValue.Of(row.MaxLength)),
            new("precision", LogValue.Of(row.Precision)),
            new("scale", LogValue.Of(row.Scale)),
            new("userTypeSchema", row.UserTypeSchema),
            new("userTypeName", row.UserTypeName),
            new("assemblyQualifiedTypeName", row.AssemblyQualifiedTypeName)
        );

    private static LogValue DescribeErrorOf(SqlServerDescribeError error) =>
        LogValue.MapOf(new("number", error.Number), new("type", error.Type), new("message", error.Message));
}

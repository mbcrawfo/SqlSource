using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// The events of the SQL Server describer, each built in one place: what a call asked and what the server answered,
/// as the capture holds it.
/// </summary>
/// <remarks>
/// The names are written without the engine: the log a describer is given puts <c>mssql.</c> before each, and removes
/// the connection's value from every string.  A member is added and never renamed.  Every event has the name of the
/// query as its first member, <c>query</c>.  The event of a call has then the SQL the call sent as <c>sql</c> and the
/// declarations it sent as <c>params</c>, or for <c>table</c>, whose text is the tool's, the <c>schema</c> and
/// <c>table</c> it describes, and last <c>durationMs</c>.  A row is a map of every member of the record, named as the
/// capture names it.  An error is the list of every error of the exception, in its order, each a map of <c>number</c>,
/// <c>class</c>, <c>state</c> and <c>message</c>; an error of the describer's own
/// (<see cref="ConnectionErrors.Own" />), such as a connection found closed or a connection string it refuses, has 0
/// for each number.  <c>open</c> has no event of its own: the run's <c>database.open</c> records what the session knows
/// of its server, the four settings included.  A describe writes <c>declared</c> for each declared parameter, in the
/// list's order, then <c>parameters</c>, then <c>probe</c> for each probe, one or two for each copy of a reused
/// parameter whose copies' types differ, then <c>columns</c>; and for a statement with rows, <c>column</c> for each
/// column in order, then <c>table</c> when every column has an origin in one table, then <c>table-match</c>.  An event
/// is written only when its call was made or its step was reached, so a describe that fails writes nothing after the
/// step that failed.  When <c>parameters</c> fails with 11501, <c>columns</c> follows it, asked with the declarations
/// alone, and nothing after.
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
            DescribedOf(answer.Error, answer.Errors, answer.Rows, DeclaredRowOf),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>mssql.columns</c>: the SQL as written and the declarations of every parameter, and every row the server
    /// described, the hidden ones included, the error rows of the describe, or the error of the call.
    /// </summary>
    public static void Columns(
        IRunLog log,
        string query,
        SqlServerColumnsRequest request,
        SqlServerColumnsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            SqlServerCallNames.Columns,
            new("query", query),
            new("sql", request.Sql),
            new("params", request.Params),
            DescribedOf(answer.Error, answer.Errors, answer.Rows, ColumnRowOf),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>mssql.table</c>: the schema and the table of the one origin, and every row the server described of
    /// <c>SELECT *</c> from it, the error rows of the describe, or the error of the call.
    /// </summary>
    public static void Table(
        IRunLog log,
        string query,
        SqlServerTableRequest request,
        SqlServerColumnsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            SqlServerCallNames.Table,
            [
                new("query", query),
                .. DescriberEvents.TableMembers(request.Schema, request.Table),
                DescribedOf(answer.Error, answer.Errors, answer.Rows, ColumnRowOf),
                new(Duration, durationMs),
            ]
        );

    /// <summary>
    /// <c>mssql.column</c>: one column as it was built from the server's row, with its origin, or why it has none.
    /// </summary>
    /// <param name="log">The describer's log.</param>
    /// <param name="query">The query's name.</param>
    /// <param name="column">The column.</param>
    /// <param name="noOriginReason">Why the column has no origin, or null for a column with one.</param>
    public static void Column(IRunLog log, string query, SidecarColumn column, string? noOriginReason) =>
        log.Write(
            "column",
            [
                .. DescriberEvents.ColumnHead(query, column),
                new("noOrigin", noOriginReason),
                new("nullable", LogValue.Of(column.Nullable)),
                new("nullableSource", column.NullableSource),
                new("identity", LogValue.Of(column.Identity)),
                new("computed", LogValue.Of(column.Computed)),
            ]
        );

    /// <summary>
    /// <c>mssql.table-match</c>: <c>matched</c> with the table or view whose column list the result is, or the first
    /// check of the match that failed with no table.  The table is its schema and its name apart, as the origin of a
    /// <c>column</c> is.
    /// </summary>
    public static void TableMatch(IRunLog log, string query, SidecarTable? table, string tableMatch) =>
        log.Write("table-match", [new("query", query), .. DescriberEvents.TableMatchTail(table, tableMatch)]);

    // The answer of a call of sys.dm_exec_describe_first_result_set: the error of the call, the error rows of the
    // describe, or its rows, each a map.
    private static LogMember DescribedOf<TRow>(
        SqlServerServerError? error,
        IReadOnlyList<SqlServerDescribeError>? errors,
        IReadOnlyList<TRow>? rows,
        Func<TRow, LogValue> rowOf
    ) =>
        (error, errors) switch
        {
            ({ } failed, _) => new LogMember(Error, ErrorOf(failed)),
            (_, { Count: > 0 } described) => new LogMember(
                "errors",
                LogValue.ListOf(described.Select(DescribeErrorOf))
            ),
            _ => new LogMember("rows", LogValue.ListOf((rows ?? []).Select(rowOf))),
        };

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

    private static LogValue ColumnRowOf(SqlServerColumnRow row) =>
        LogValue.MapOf(
            new("isHidden", row.IsHidden),
            new("columnOrdinal", row.ColumnOrdinal),
            new("name", row.Name),
            new("isNullable", LogValue.Of(row.IsNullable)),
            new("systemTypeName", row.SystemTypeName),
            new("maxLength", LogValue.Of(row.MaxLength)),
            new("precision", LogValue.Of(row.Precision)),
            new("scale", LogValue.Of(row.Scale)),
            new("userTypeSchema", row.UserTypeSchema),
            new("userTypeName", row.UserTypeName),
            new("assemblyQualifiedTypeName", row.AssemblyQualifiedTypeName),
            new("sourceServer", row.SourceServer),
            new("sourceDatabase", row.SourceDatabase),
            new("sourceSchema", row.SourceSchema),
            new("sourceTable", row.SourceTable),
            new("sourceColumn", row.SourceColumn),
            new("isIdentityColumn", LogValue.Of(row.IsIdentityColumn)),
            new("isComputedColumn", LogValue.Of(row.IsComputedColumn)),
            new("sourceType", row.SourceType)
        );

    private static LogValue DescribeErrorOf(SqlServerDescribeError error) =>
        LogValue.MapOf(new("number", error.Number), new("type", error.Type), new("message", error.Message));
}

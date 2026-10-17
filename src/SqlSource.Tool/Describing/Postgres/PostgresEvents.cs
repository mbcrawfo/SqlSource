using System;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Walk;
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
/// writes <c>prepare</c>, <c>columns</c>, <c>tables</c>, <c>types</c>, <c>spellings</c>, one <c>column</c> for each
/// column, <c>check</c>, <c>table-match</c> and <c>deallocate</c>, in that order, each only when its call was made,
/// and <c>table-match</c> only for a statement with rows whose check agreed.  A query with rows that is gated or has
/// a column of no origin writes, between <c>check</c> and <c>table-match</c>, <c>explain</c>, <c>partitions</c> and
/// <c>relations</c> when each is asked, one <c>walk.node</c> for each node the walk visited, one
/// <c>walk.column</c> for each column, and <c>walk</c>.
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

    /// <summary>
    /// <c>postgres.spellings</c>: each type and modifier asked for, and the server's spelling of each, or the error.
    /// </summary>
    public static void Spellings(
        IRunLog log,
        PostgresSpellingsRequest request,
        PostgresSpellingsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "spellings",
            new(
                "pairs",
                LogValue.ListOf(
                    request.Pairs.Select(static pair =>
                        LogValue.MapOf(new("oid", pair.Oid), new("modifier", pair.Modifier))
                    )
                )
            ),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember(
                    "rows",
                    LogValue.ListOf(
                        (answer.Rows ?? []).Select(static row =>
                            LogValue.MapOf(
                                new("oid", row.Oid),
                                new("modifier", row.Modifier),
                                new("formatType", row.FormatType)
                            )
                        )
                    )
                ),
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
    /// <c>postgres.check</c>: the name and the type name of each parameter, the <c>result</c>, which is
    /// <c>same</c>, <c>differ</c>, <c>unbound</c> or <c>error</c> (<see cref="NpgsqlCheck" />), and <c>differs</c>:
    /// what differs, Npgsql's message of a type it cannot bind, or the message of an <c>error</c> result.  The SQL is
    /// in <c>postgres.prepare</c>.
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
    /// <c>postgres.column</c>: one column as it was built from the catalog, and whether its query was gated.  For a
    /// query the walk planned, the nullability recorded is the one of <c>walk.column</c>.
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
            new("nullable", LogValue.Of(column.Nullable)),
            new("nullableSource", column.NullableSource),
            new("gated", gated)
        );

    /// <summary>
    /// <c>postgres.table-match</c>: <c>matched</c> with the relation whose column list the result is, or the first
    /// check of the match that failed with no table.  The relation is its schema and its name apart, as the origin
    /// of a <c>column</c> is: a name may hold a dot.
    /// </summary>
    public static void TableMatch(IRunLog log, string query, SidecarTable? table, string result) =>
        log.Write(
            "table-match",
            new("query", query),
            new("result", result),
            new(
                "table",
                table is null ? LogValue.Null : LogValue.MapOf(new("schema", table.Schema), new("table", table.Table))
            )
        );

    /// <summary>
    /// <c>postgres.explain</c>: the <c>EXPLAIN</c> as it was sent, with the settings it ran under, and the plan's
    /// text or the error.  The log's writer cuts a string at 1 MiB.
    /// </summary>
    public static void Explain(
        IRunLog log,
        string query,
        PostgresExplainRequest request,
        PostgresExplainAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "explain",
            new("query", query),
            new("statement", request.Statement),
            new("settings", LogValue.ListOfTexts(request.Settings)),
            new("sql", PostgresLive.ExplainText(request)),
            answer.Error is { } error ? new LogMember(Error, ErrorOf(error)) : new LogMember("plan", answer.Plan),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.partitions</c>: the relations the members of the plan's <c>Append</c>s scan, and the ancestors of
    /// each or the error.  An error leaves those <c>Append</c>s as the walk reads any other.
    /// </summary>
    public static void Partitions(
        IRunLog log,
        string query,
        PostgresPartitionsRequest request,
        PostgresPartitionsAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "partitions",
            new("query", query),
            new("relations", LogValue.ListOf(request.Relations.Select(RelationOf))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("rows", LogValue.ListOf((answer.Rows ?? []).Select(PartitionOf))),
            new(Duration, durationMs)
        );

    /// <summary>
    /// <c>postgres.relations</c>: the relations a plan scans that <c>tables</c> did not answer, and the rows of those
    /// that exist, as <c>tables</c> holds them, or the error.
    /// </summary>
    public static void Relations(
        IRunLog log,
        string query,
        PostgresRelationsRequest request,
        PostgresTablesAnswer answer,
        long durationMs
    ) =>
        log.Write(
            "relations",
            new("query", query),
            new("relations", LogValue.ListOf(request.Relations.Select(RelationOf))),
            answer.Error is { } error
                ? new LogMember(Error, ErrorOf(error))
                : new LogMember("rows", LogValue.ListOf((answer.Tables ?? []).Select(TableOf))),
            new(Duration, durationMs)
        );

    /// <summary>
    /// The events of a walk, in this order: <c>postgres.walk.node</c> for each node that was visited,
    /// <c>postgres.walk.column</c> for each column, and <c>postgres.walk</c> with the plan's value and the reason when
    /// there is one: why the plan is <c>unavailable</c>, or <c>output-differs</c> for a <c>walked</c> one.  It is the
    /// one place that tells a failed <c>EXPLAIN</c> from a plan the walk could not use.
    /// </summary>
    /// <remarks>
    /// The relation of a node is its schema and its name apart, as the table of <c>table-match</c> is: a name may
    /// hold a dot.
    /// </remarks>
    public static void Walk(IRunLog log, string query, WalkOutcome outcome)
    {
        foreach (var visited in outcome.Nodes)
        {
            var node = visited.Node;
            log.Write(
                "walk.node",
                new("depth", visited.Depth),
                new("nodeType", node.NodeType),
                new("joinType", node.JoinType),
                new("parentRelationship", node.ParentRelationship),
                new("alias", node.Alias),
                new(
                    "relation",
                    node.RelationName is { } relation
                        ? LogValue.MapOf(new("schema", node.Schema), new("table", relation))
                        : LogValue.Null
                ),
                new("nullExtends", TextOf(visited.NullExtends))
            );
        }

        foreach (var walked in outcome.Columns)
        {
            var column = walked.Column;
            log.Write(
                "walk.column",
                new("ordinal", column.Ordinal),
                new("name", column.Name),
                new("output", walked.Output),
                new("alias", walked.Reference?.Alias),
                new("column", walked.Reference?.Column),
                new("mapped", walked.Mapped),
                new("nullExtended", walked.NullExtended),
                new("nullable", LogValue.Of(column.Nullable)),
                new("nullableSource", column.NullableSource)
            );
        }

        log.Write("walk", new("query", query), new("plan", outcome.Plan), new("reason", outcome.Reason));
    }

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

    /// <summary>
    /// The <c>nullExtends</c> of <c>postgres.walk.node</c>: <c>none</c>, <c>inner</c>, <c>outer</c> or <c>both</c>.
    /// Spelled here and not from the member's name, so that renaming a member cannot change the log.
    /// </summary>
    public static string TextOf(NullExtension extension) =>
        extension switch
        {
            NullExtension.None => "none",
            NullExtension.Inner => "inner",
            NullExtension.Outer => "outer",
            NullExtension.Both => "both",
            _ => throw new InvalidOperationException($"Unknown null extension {extension}."),
        };

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
            new("scale", LogValue.Of(column.Scale)),
            new("typeModifier", LogValue.Of(column.TypeModifier))
        );

    private static LogValue RelationOf(PostgresRelationName relation) =>
        LogValue.MapOf(new("schema", relation.Schema), new("table", relation.Name));

    private static LogValue PartitionOf(PostgresPartitionRow row) =>
        LogValue.MapOf(
            new("schema", row.Schema),
            new("table", row.Name),
            new("ancestors", LogValue.ListOf(row.Ancestors.Select(static oid => (LogValue)oid)))
        );

    private static LogValue TableOf(PostgresTableRow table) =>
        LogValue.MapOf(
            new("oid", table.Oid),
            new("relkind", table.Relkind),
            new("schema", table.Schema),
            new("name", table.Name),
            new("columns", LogValue.ListOf(table.Columns.Select(AttributeOf))),
            new("relhassubclass", LogValue.Of(table.Relhassubclass)),
            new("hasForeignPartition", LogValue.Of(table.HasForeignPartition)),
            new("hasInsteadRule", LogValue.Of(table.HasInsteadRule))
        );

    private static LogValue AttributeOf(PostgresAttributeRow attribute) =>
        LogValue.MapOf(
            new("attnum", attribute.Attnum),
            new("name", attribute.Name),
            new("notNull", attribute.NotNull),
            new("identity", attribute.Identity),
            new("generated", attribute.Generated),
            new("typeOid", attribute.TypeOid),
            new("notNullValidated", LogValue.Of(attribute.NotNullValidated))
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
            new("enumLabels", row.EnumLabels is { } labels ? LogValue.ListOfTexts(labels) : LogValue.Null),
            new("typtypmod", LogValue.Of(row.Typtypmod))
        );
}

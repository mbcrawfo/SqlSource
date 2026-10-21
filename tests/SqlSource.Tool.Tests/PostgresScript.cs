using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Tests;

// What a PostgreSQL would answer, written by a test: each call the describer is to make, with its answer.  Play
// gives the exchange that answers from it, with no database.  A request the describer asks otherwise than the test
// wrote it has no answer and is a CaptureMissException, so a test of this kind holds the requests too.
internal sealed class PostgresScript
{
    public const string Database = "app";

    // No connection string: under a replay nothing reads it.
    public const string Connection = FixtureReplay.Connection;

    private readonly DescribeCapture _capture = new(SidecarValues.Engine.Postgres);

    // What "open" answers for a server that opened.
    public static PostgresOpenAnswer Opened { get; } =
        new(
            "18.4",
            "10.0.3",
            [
                new PostgresSetting("search_path", "\"$user\", public"),
                new PostgresSetting("plan_cache_mode", "auto"),
                new PostgresSetting("standard_conforming_strings", "on"),
            ],
            null
        );

    // What "columns" answers for a statement without rows.
    public static PostgresColumnsAnswer NoColumns { get; } = new([], null);

    public PostgresScript Answer<TRequest, TAnswer>(string method, TRequest request, TAnswer answer)
    {
        _capture.Add(new CaptureCall(method, CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        return this;
    }

    public PostgresScript Open() => Answer("open", PostgresOpenRequest.Instance, Opened);

    // The name the session gives the statement of its describe of that number in the session.
    public static string Statement(int number) => $"sqlsource_{number}";

    // What "deallocate" answers for a statement that was freed.
    public static PostgresDeallocateAnswer Freed { get; } = new(null);

    // What "explain" answers for a statement whose column of no origin makes the describe ask for the plan, in a
    // test of something else: the server gives none, and every column keeps what it was built with.
    public static PostgresExplainAnswer NoPlan { get; } =
        new(null, new PostgresServerError("XX000", "no plan in this test", null, null, null));

    public static PostgresExplainAnswer PlanOf(PlanNode root) => new(PostgresPlans.Text(root), null);

    // What "check" answers when it sees the columns the test gives, each with the type "columns" answered: the two
    // describes agree.
    public static PostgresCheckAnswer Agreeing(params PostgresColumnRow[] columns) =>
        new(
            [.. columns.Select(static column => new PostgresCheckColumn(column.Name, column.TypeOid, "a type"))],
            null,
            null
        );

    // "prepare" alone: for a describe that ends there, and for one whose columns the test writes.
    public PostgresScript Prepares(int number, string sql, string[] types, PostgresPrepareAnswer answer) =>
        Answer("prepare", new PostgresPrepareRequest(Statement(number), sql, types), answer);

    // "prepare", and "columns" answering no column when the statement was prepared with a type for each parameter
    // of the list: most of the tests that use it are about the parameters.
    public PostgresScript PreparesWithNoColumns(int number, string sql, string[] types, PostgresPrepareAnswer answer)
    {
        _ = Prepares(number, sql, types, answer);
        return answer.ParameterTypes is { } resolved && resolved.Count >= types.Length
            ? Columns(number, types.Length)
            : this;
    }

    public PostgresScript Columns(int number, int parameters, params PostgresColumnRow[] columns) =>
        Answer(
            "columns",
            new PostgresColumnsRequest(Statement(number), parameters),
            new PostgresColumnsAnswer(columns, null)
        );

    public PostgresScript Frees(int number, PostgresDeallocateAnswer? answer = null) =>
        Answer("deallocate", new PostgresDeallocateRequest(Statement(number)), answer ?? Freed);

    // "check" answering the columns the test gives: see Agreeing.
    public PostgresScript Checks(
        string sql,
        (string Name, string Type)[] parameters,
        params PostgresColumnRow[] columns
    ) =>
        Answer(
            "check",
            new PostgresCheckRequest(
                sql,
                [.. parameters.Select(static parameter => new PostgresCheckParameter(parameter.Name, parameter.Type))]
            ),
            Agreeing(columns)
        );

    // "explain" for the statement, with no setting, answering what the test gives.
    public PostgresScript Explains(int number, int parameters, PostgresExplainAnswer answer) =>
        Answer("explain", new PostgresExplainRequest(Statement(number), parameters, []), answer);

    // "explain" answering NoPlan.
    public PostgresScript PlansNothing(int number, int parameters) => Explains(number, parameters, NoPlan);

    public PostgresScript Resolves(long[] oids, params PostgresTypeRow[] rows) =>
        Answer("types", new PostgresTypesRequest(oids), new PostgresTypesAnswer(rows, null));

    public PostgresScript Tables(long[] oids, params PostgresTableRow[] tables) =>
        Answer("tables", new PostgresTablesRequest(oids), new PostgresTablesAnswer(tables, null));

    // "spellings" answering the server's text for each pair the test gives, in the order it gives them.
    public PostgresScript Spells(params (long Oid, int Modifier, string FormatType)[] rows) =>
        Answer(
            "spellings",
            new PostgresSpellingsRequest([.. rows.Select(static row => new PostgresTypmodPair(row.Oid, row.Modifier))]),
            new PostgresSpellingsAnswer(
                [.. rows.Select(static row => new PostgresSpellingRow(row.Oid, row.Modifier, row.FormatType))],
                null
            )
        );

    public PostgresScript Partitions(PostgresPartitionsRequest request, PostgresPartitionsAnswer answer) =>
        Answer("partitions", request, answer);

    // "relations" answers tables, as "tables" does.
    public PostgresScript Relations(PostgresRelationsRequest request, PostgresTablesAnswer answer) =>
        Answer("relations", request, answer);

    // "relations" for the relations the test names, in schema and name order, answering the rows it gives.
    public PostgresScript Relations(IReadOnlyList<PostgresRelationName> names, params PostgresTableRow[] tables) =>
        Relations(new PostgresRelationsRequest(names), new PostgresTablesAnswer(tables, null));

    // Made when the script is whole.
    public ReplayingExchange Play() => new(_capture);

    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        string connection = Connection,
        IReadOnlyList<string>? explainSettings = null,
        TimeProvider? time = null
    ) => new PostgresDescriber(explainSettings ?? []).OpenAsync(Database, connection, exchange, log, time);

    // The session of a script whose "open" answered a server.
    public static async Task<IDescribeSession> SessionAsync(
        IDescribeExchange exchange,
        IRunLog? log = null,
        IReadOnlyList<string>? explainSettings = null,
        TimeProvider? time = null
    ) => (await OpenAsync(exchange, log, explainSettings: explainSettings, time: time)).Value.ShouldNotBeNull();
}

using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says of each call in the run's log: what it asked and what the server answered.
public class PostgresEventsTests
{
    private static readonly DescribeRequest Query = new(
        "GetUser",
        "SELECT 1 FROM users WHERE id = @id",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("id", null, null, false)])
    );

    // Query as the session prepares it.
    private const string PreparedSql = "SELECT 1 FROM users WHERE id = $1";

    private static readonly PostgresColumnsRequest Columns = new(PostgresScript.Statement(1), 1);

    private static readonly PostgresCheckRequest Check = new(
        "SELECT 1 FROM users WHERE id = @id",
        [new PostgresCheckParameter("id", "integer")]
    );

    private static LogValue Member(LogMember[] members, string name) =>
        members.Single(member => member.Name == name).Value;

    private static string[] NamesOf(LogMember[] members) => [.. members.Select(member => member.Name)];

    private static async Task DescribeAsync(PostgresScript script, IRunLog log, DescribeRequest? query = null)
    {
        await using var session = await PostgresScript.SessionAsync(script.Play(), log);
        _ = await session.DescribeAsync(query ?? Query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks("SELECT 1 FROM users WHERE id = @id", [("id", "integer")])
            .Frees(1);

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "check", "deallocate"]);

        var prepare = log.Events[0].Members;
        NamesOf(prepare).ShouldBe(["query", "statement", "sql", "types", "parameterTypes", "durationMs"]);
        Member(prepare, "query").ShouldBe((LogValue)"GetUser");
        Member(prepare, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(prepare, "sql").ShouldBe((LogValue)PreparedSql);
        Member(prepare, "types").ShouldBe(LogValue.ListOfTexts(["unknown"]));
        Member(prepare, "parameterTypes").ShouldBe(LogValue.ListOf([23L]));
        _ = Member(prepare, "durationMs").ShouldBeOfType<LogValue.Number>();

        var types = log.Events[2].Members;
        NamesOf(types).ShouldBe(["oids", "rows", "durationMs"]);
        Member(types, "oids").ShouldBe(LogValue.ListOf([23L]));
        Member(types, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("oid", 23L),
                        new("typname", "int4"),
                        new("nspname", "pg_catalog"),
                        new("typtype", "b"),
                        new("typcategory", "N"),
                        new("typelem", 0L),
                        new("typbasetype", 0L),
                        new("rngsubtype", LogValue.Null),
                        new("multirangeRngsubtype", LogValue.Null),
                        new("formatType", "integer"),
                        new("enumLabels", LogValue.Null),
                        new("typtypmod", LogValue.Null)
                    ),
                ])
            );

        var deallocate = log.Events[4].Members;
        NamesOf(deallocate).ShouldBe(["statement", "error", "durationMs"]);
        Member(deallocate, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(deallocate, "error").ShouldBe(LogValue.Null);
    }

    // A prepare that failed has its error in the place of parameterTypes, and nothing else is asked or written.
    [Fact]
    public async Task Describe_PrepareThatFails_WritesItsErrorAndNoOtherEvent()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", null, 49);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer(null, rejected));

        await DescribeAsync(script, log);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("prepare");
        NamesOf(members).ShouldBe(["query", "statement", "sql", "types", "error", "durationMs"]);
        Member(members, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "42P01"),
                    new("message", "relation \"users\" does not exist"),
                    new("detail", "a detail"),
                    new("hint", LogValue.Null),
                    new("position", 49L)
                )
            );
    }

    // A column holds the modifier of the row description, or null for a capture recorded before it was read.
    [Fact]
    public void Columns_ColumnsWithAndWithoutAModifier_WritesTheModifierOrNull()
    {
        var log = new RecordingLog();
        var answer = new PostgresColumnsAnswer(
            [
                PostgresTables.ExpressionOf(0, "name", PostgresRows.Varchar.Oid, 32, null, null, 36),
                PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid),
            ],
            null
        );

        PostgresEvents.Columns(log, "GetUser", Columns, answer, 1);

        Member(log.Events.ShouldHaveSingleItem().Members, "columns")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(
                        new("ordinal", 0L),
                        new("name", "name"),
                        new("typeOid", 1043L),
                        new("tableOid", 0L),
                        new("attributeNumber", 0L),
                        new("length", 32L),
                        new("precision", LogValue.Null),
                        new("scale", LogValue.Null),
                        new("typeModifier", 36L)
                    ),
                    LogValue.MapOf(
                        new("ordinal", 1L),
                        new("name", "n"),
                        new("typeOid", 20L),
                        new("tableOid", 0L),
                        new("attributeNumber", 0L),
                        new("length", LogValue.Null),
                        new("precision", LogValue.Null),
                        new("scale", LogValue.Null),
                        new("typeModifier", LogValue.Null)
                    ),
                ])
            );
    }

    // The event of "spellings" is built from what it is given; the tests of the session say when it is asked.
    [Fact]
    public void Spellings_RowsOfTheServer_WritesThePairsAndTheRows()
    {
        var log = new RecordingLog();
        var request = new PostgresSpellingsRequest([
            new PostgresTypmodPair(1043, 36),
            new PostgresTypmodPair(1700, -1),
        ]);
        var answer = new PostgresSpellingsAnswer(
            [new PostgresSpellingRow(1043, 36, "character varying(32)"), new PostgresSpellingRow(1700, -1, "numeric")],
            null
        );

        PostgresEvents.Spellings(log, request, answer, 3);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("spellings");
        NamesOf(members).ShouldBe(["pairs", "rows", "durationMs"]);
        Member(members, "pairs")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(new("oid", 1043L), new("modifier", 36L)),
                    LogValue.MapOf(new("oid", 1700L), new("modifier", -1L)),
                ])
            );
        Member(members, "rows")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(new("oid", 1043L), new("modifier", 36L), new("formatType", "character varying(32)")),
                    LogValue.MapOf(new("oid", 1700L), new("modifier", -1L), new("formatType", "numeric")),
                ])
            );
        Member(members, "durationMs").ShouldBe((LogValue)3L);
    }

    // A spellings call that failed has its error in the place of the rows.
    [Fact]
    public void Spellings_ErrorOfTheServer_WritesItInThePlaceOfTheRows()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError(
            "57014",
            "canceling statement due to statement timeout",
            null,
            null,
            null
        );

        PostgresEvents.Spellings(
            log,
            new PostgresSpellingsRequest([new PostgresTypmodPair(1043, 36)]),
            new PostgresSpellingsAnswer(null, refused),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["pairs", "error", "durationMs"]);
        Member(members, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "57014"),
                    new("message", "canceling statement due to statement timeout"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
    }

    // A types call that failed has its error in the place of the rows.
    [Fact]
    public async Task Describe_TypesThatFails_WritesItsErrorInThePlaceOfTheRows()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError(
            "57014",
            "canceling statement due to statement timeout",
            null,
            null,
            null
        );
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, refused))
            .Frees(1);

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "deallocate"]);
        var types = log.Events[2].Members;
        NamesOf(types).ShouldBe(["oids", "error", "durationMs"]);
        Member(types, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "57014"),
                    new("message", "canceling statement due to statement timeout"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
    }

    // The error of "deallocate" is nowhere but here.
    [Fact]
    public async Task Describe_DeallocateThatFails_WritesTheErrorThatIsNeverReported()
    {
        var log = new RecordingLog();
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks("SELECT 1 FROM users WHERE id = @id", [("id", "integer")])
            .Frees(1, new PostgresDeallocateAnswer(gone));

        await DescribeAsync(script, log);

        var error = Member(log.Events[^1].Members, "error").ShouldBeOfType<LogValue.Members>();
        error.Values[0].ShouldBe(new LogMember("sqlState", "26000"));
    }

    // As a run gives the describer its log: the engine before each name, and the connection's value written as ***
    // wherever a describer put it, the SQL of a query included.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks("SELECT 1 FROM users WHERE id = @id", [("id", "integer")])
            .Frees(1);

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."));

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "postgres.prepare",
                "postgres.columns",
                "postgres.types",
                "postgres.check",
                "postgres.deallocate",
            ]);
        Member(log.Events[0].Members, "sql").ShouldBe((LogValue)"SELECT 1 FROM *** WHERE id = $1");
    }

    // A query of users.id and an aggregate: one column with an origin and one without.
    private static readonly DescribeRequest Listing = new(
        "ListUsers",
        "SELECT id, count(*) OVER () AS n FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static readonly PostgresColumnRow CountColumn = PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid);

    // The script of Listing without "check" and "explain", which a test adds as it needs.
    private static PostgresScript ListingScript() =>
        new PostgresScript()
            .Open()
            .Prepares(1, Listing.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, PostgresTables.UsersId, CountColumn)
            .Tables([PostgresTables.Users.Oid], PostgresTables.Users)
            .Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4)
            .Frees(1);

    [Fact]
    public async Task Describe_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId, CountColumn).PlansNothing(1, 0);

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "check",
                "explain",
                "walk.column",
                "walk.column",
                "walk",
                "table-match",
                "deallocate",
            ]);

        var columns = log.Events[1].Members;
        NamesOf(columns).ShouldBe(["query", "statement", "columns", "durationMs"]);
        Member(columns, "query").ShouldBe((LogValue)"ListUsers");
        Member(columns, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(columns, "columns")
            .ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBe(
                LogValue.MapOf(
                    new("ordinal", 0L),
                    new("name", "id"),
                    new("typeOid", 23L),
                    new("tableOid", PostgresTables.Users.Oid),
                    new("attributeNumber", 1L),
                    new("length", LogValue.Null),
                    new("precision", LogValue.Null),
                    new("scale", LogValue.Null),
                    new("typeModifier", LogValue.Null)
                )
            );

        var tables = log.Events[2].Members;
        NamesOf(tables).ShouldBe(["oids", "tables", "durationMs"]);
        Member(tables, "oids").ShouldBe(LogValue.ListOf([PostgresTables.Users.Oid]));
        var users = Member(tables, "tables")
            .ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBeOfType<LogValue.Members>();
        users
            .Values.Select(value => value.Name)
            .ShouldBe([
                "oid",
                "relkind",
                "schema",
                "name",
                "columns",
                "relhassubclass",
                "hasForeignPartition",
                "hasInsteadRule",
            ]);
        users.Values[5].Value.ShouldBe((LogValue)false);
        users.Values[6].Value.ShouldBe((LogValue)false);
        users.Values[7].Value.ShouldBe((LogValue)false);
        users
            .Values[4]
            .Value.ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBe(
                LogValue.MapOf(
                    new("attnum", 1L),
                    new("name", "id"),
                    new("notNull", true),
                    new("identity", "a"),
                    new("generated", string.Empty),
                    new("typeOid", 23L),
                    new("notNullValidated", LogValue.Null)
                )
            );

        var id = log.Events[4].Members;
        NamesOf(id).ShouldBe(["query", "ordinal", "name", "type", "origin", "nullable", "nullableSource", "gated"]);
        Member(id, "query").ShouldBe((LogValue)"ListUsers");
        Member(id, "ordinal").ShouldBe((LogValue)0L);
        Member(id, "name").ShouldBe((LogValue)"id");
        Member(id, "type").ShouldBe((LogValue)"integer");
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "users"), new("column", "id")));
        Member(id, "nullable").ShouldBe((LogValue)false);
        Member(id, "nullableSource").ShouldBe((LogValue)"catalog");
        Member(id, "gated").ShouldBe((LogValue)false);

        var count = log.Events[5].Members;
        Member(count, "origin").ShouldBe(LogValue.Null);
        Member(count, "nullable").ShouldBe(LogValue.Null);
        Member(count, "nullableSource").ShouldBe((LogValue)"no-origin");

        var check = log.Events[6].Members;
        NamesOf(check).ShouldBe(["query", "parameters", "result", "differs", "durationMs"]);
        Member(check, "result").ShouldBe((LogValue)"same");
        Member(check, "differs").ShouldBe(LogValue.Null);

        var match = log.Events[11].Members;
        NamesOf(match).ShouldBe(["query", "result", "table"]);
        Member(match, "query").ShouldBe((LogValue)"ListUsers");
        Member(match, "result").ShouldBe((LogValue)"no-origin");
        Member(match, "table").ShouldBe(LogValue.Null);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesItsNameAndTypeNameInCheck()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks("SELECT 1 FROM users WHERE id = @id", [("id", "integer")])
            .Frees(1);

        await DescribeAsync(script, log);

        Member(log.Events.Single(written => written.Name == "check").Members, "parameters")
            .ShouldBe(LogValue.ListOf([LogValue.MapOf(new("name", "id"), new("dataTypeName", "integer"))]));
    }

    [Fact]
    public async Task Describe_CheckThatDiffers_WritesWhatDiffers()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId);

        await DescribeAsync(script, log, Listing);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"differ");
        Member(check, "differs").ShouldBe((LogValue)"the result has 1 columns for Npgsql and 2 for SqlSource");
    }

    [Fact]
    public async Task Describe_CheckWithATypeThatCannotBeBound_SaysUnboundWithTheDriversMessage()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Answer("check", Check, new PostgresCheckAnswer(null, null, new PostgresBindError("id", "no such type")))
            .Frees(1);

        await DescribeAsync(script, log);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"unbound");
        Member(check, "differs").ShouldBe((LogValue)"no such type");
    }

    // An error of check that says nothing of how Npgsql reads the SQL is no disagreement.
    [Fact]
    public async Task Describe_CheckThatTheServerCancels_SaysErrorWithItsMessage()
    {
        var log = new RecordingLog();
        var canceled = new PostgresServerError("57014", "canceling statement due to user request", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Answer("check", Check, new PostgresCheckAnswer(null, canceled, null, null, false))
            .Frees(1);

        await DescribeAsync(script, log);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"error");
        Member(check, "differs").ShouldBe((LogValue)"canceling statement due to user request");
    }

    // A columns call that failed has its error in the place of the columns.
    [Fact]
    public async Task Describe_ColumnsThatFails_WritesItsErrorInThePlaceOfTheColumns()
    {
        var log = new RecordingLog();
        var lost = new PostgresServerError(null, "the connection to the server was lost", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, new PostgresColumnsAnswer(null, lost))
            .Frees(1);

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "deallocate"]);
        NamesOf(log.Events[1].Members).ShouldBe(["query", "statement", "error", "durationMs"]);
    }

    [Fact]
    public async Task Describe_TablesThatFails_WritesItsErrorInThePlaceOfTheTables()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied for table pg_attribute", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, Listing.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, PostgresTables.UsersId)
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid]),
                new PostgresTablesAnswer(null, refused)
            )
            .Frees(1);

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "tables", "deallocate"]);
        NamesOf(log.Events[2].Members).ShouldBe(["oids", "error", "durationMs"]);
    }

    // The describe ends at the first column of a pseudo-type: the columns before it have their events, and it and
    // the ones after it have none.
    [Fact]
    public async Task Describe_ColumnOfAPseudoType_WritesAColumnForTheColumnsBeforeItAlone()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, Listing.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(
                1,
                0,
                PostgresTables.ExpressionOf(0, "one", PostgresRows.Int4.Oid),
                PostgresTables.ExpressionOf(1, "pair", PostgresRows.Record.Oid),
                PostgresTables.ExpressionOf(2, "two", PostgresRows.Int4.Oid)
            )
            .Resolves([23, 2249], PostgresRows.Int4, PostgresRows.Record)
            .Frees(1);

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "column", "deallocate"]);
        Member(log.Events[3].Members, "name").ShouldBe((LogValue)"one");
    }

    // A column's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId, CountColumn).PlansNothing(1, 0);

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."), Listing);

        var id = log.Events.First(written => written.Name == "postgres.column").Members;
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***"), new("column", "id")));
    }

    // A plan over an Append of two partitions asks their ancestors between "explain" and the walk, and the event says
    // what was asked and what was answered.
    [Fact]
    public async Task Describe_PlanWithAnAppendOfPartitions_WritesPartitionsBeforeTheWalk()
    {
        var log = new RecordingLog();
        var plan = Over(
            "WindowAgg",
            Append(PostgresPlans.Member("u_1", "users_1", "u_1.id"), PostgresPlans.Member("u_2", "users_2", "u_2.id"))
        ) with
        {
            Output = ["u.id", "count(*) OVER w1"],
        };
        var asked = new PostgresPartitionsRequest([
            new PostgresRelationName("public", "users_1"),
            new PostgresRelationName("public", "users_2"),
        ]);
        var script = ListingScript()
            .Checks(Listing.Sql, [], PostgresTables.UsersId, CountColumn)
            .Explains(1, 0, new PostgresExplainAnswer(Text(plan), null))
            .Partitions(
                asked,
                new PostgresPartitionsAnswer(
                    [
                        new("public", "users_1", [PostgresTables.Users.Oid]),
                        new("public", "users_2", [16999, PostgresTables.Users.Oid]),
                    ],
                    null
                )
            );

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .SkipWhile(static name => name != "explain")
            .Take(3)
            .ShouldBe(["explain", "partitions", "walk.node"]);
        var partitions = log.Events.Single(static written => written.Name == "partitions").Members;
        NamesOf(partitions).ShouldBe(["query", "relations", "rows", "durationMs"]);
        Member(partitions, "query").ShouldBe((LogValue)"ListUsers");
        Member(partitions, "relations")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(new("schema", "public"), new("table", "users_1")),
                    LogValue.MapOf(new("schema", "public"), new("table", "users_2")),
                ])
            );
        Member(partitions, "rows")
            .ShouldBeOfType<LogValue.Items>()
            .Values[1]
            .ShouldBe(
                LogValue.MapOf(
                    new("schema", "public"),
                    new("table", "users_2"),
                    new("ancestors", LogValue.ListOf([16999L, PostgresTables.Users.Oid]))
                )
            );
    }

    [Fact]
    public async Task Describe_PartitionsThatFails_WritesItsErrorInThePlaceOfTheRows()
    {
        var log = new RecordingLog();
        var plan = Append(PostgresPlans.Member("u_1", "users_1", "u_1.id")) with { Output = ["u.id", "1"] };
        var script = ListingScript()
            .Checks(Listing.Sql, [], PostgresTables.UsersId, CountColumn)
            .Explains(1, 0, new PostgresExplainAnswer(Text(plan), null))
            .Partitions(
                new PostgresPartitionsRequest([new PostgresRelationName("public", "users_1")]),
                new PostgresPartitionsAnswer(
                    null,
                    new PostgresServerError("42501", "permission denied", null, null, null)
                )
            )
            // The Append is then read as a set operation, whose branch's relation is asked for.
            .Relations(
                new PostgresRelationsRequest([new PostgresRelationName("public", "users_1")]),
                new PostgresTablesAnswer([], null)
            );

        await DescribeAsync(script, log, Listing);

        var partitions = log.Events.Single(static written => written.Name == "partitions").Members;
        NamesOf(partitions).ShouldBe(["query", "relations", "error", "durationMs"]);
        _ = Member(partitions, "error").ShouldBeOfType<LogValue.Members>();
    }

    // The event of "relations" is built from what it is given; the tests of the session say when it is asked.
    [Fact]
    public void Relations_RowsOfTheServer_WritesTheRelationsAndTheRows()
    {
        var log = new RecordingLog();
        var request = new PostgresRelationsRequest([
            new PostgresRelationName("public", "orders"),
            new PostgresRelationName("public", "users"),
        ]);
        var answer = new PostgresTablesAnswer([PostgresTables.Users], null);

        PostgresEvents.Relations(log, "ListUsers", request, answer, 4);

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("relations");
        NamesOf(members).ShouldBe(["query", "relations", "rows", "durationMs"]);
        Member(members, "query").ShouldBe((LogValue)"ListUsers");
        Member(members, "relations")
            .ShouldBe(
                LogValue.ListOf([
                    LogValue.MapOf(new("schema", "public"), new("table", "orders")),
                    LogValue.MapOf(new("schema", "public"), new("table", "users")),
                ])
            );
        var users = Member(members, "rows")
            .ShouldBeOfType<LogValue.Items>()
            .Values.ShouldHaveSingleItem()
            .ShouldBeOfType<LogValue.Members>();
        users
            .Values.Select(value => value.Name)
            .ShouldBe([
                "oid",
                "relkind",
                "schema",
                "name",
                "columns",
                "relhassubclass",
                "hasForeignPartition",
                "hasInsteadRule",
            ]);
        users.Values[3].Value.ShouldBe((LogValue)"users");
        Member(members, "durationMs").ShouldBe((LogValue)4L);
    }

    // A relations call that failed has its error in the place of the rows.
    [Fact]
    public void Relations_ErrorOfTheServer_WritesItInThePlaceOfTheRows()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError("42501", "permission denied", null, null, null);

        PostgresEvents.Relations(
            log,
            "ListUsers",
            new PostgresRelationsRequest([new PostgresRelationName("public", "users")]),
            new PostgresTablesAnswer(null, refused),
            0
        );

        var members = log.Events.ShouldHaveSingleItem().Members;
        NamesOf(members).ShouldBe(["query", "relations", "error", "durationMs"]);
        Member(members, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "42501"),
                    new("message", "permission denied"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
    }

    // A table of one column, whole in the answer of "tables", and the query that is the table.  It says nothing of a
    // foreign partition or of an INSTEAD rule, as an answer recorded before the members existed.
    private static readonly PostgresTableRow Ids = PostgresTables.TableOf(
        16500,
        "users",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
    ) with
    {
        HasForeignPartition = null,
        HasInsteadRule = null,
    };

    private static readonly DescribeRequest Whole = new(
        "ListIds",
        "SELECT * FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static PostgresScript WholeScript()
    {
        var id = PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, Ids, 1);
        return new PostgresScript()
            .Open()
            .Prepares(1, Whole.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, id)
            .Tables([Ids.Oid], Ids)
            .Resolves([23], PostgresRows.Int4)
            .Checks(Whole.Sql, [], id)
            .Frees(1);
    }

    [Fact]
    public async Task Describe_QueryThatIsATable_WritesTheTableInTableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), log, Whole);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "check", "table-match", "deallocate"]);
        var ids = Member(log.Events[2].Members, "tables")
            .ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBeOfType<LogValue.Members>();
        ids.Values[6].Name.ShouldBe("hasForeignPartition");
        ids.Values[6].Value.ShouldBe(LogValue.Null);
        ids.Values[7].Name.ShouldBe("hasInsteadRule");
        ids.Values[7].Value.ShouldBe(LogValue.Null);
        var match = log.Events[6].Members;
        Member(match, "query").ShouldBe((LogValue)"ListIds");
        Member(match, "result").ShouldBe((LogValue)"matched");
        Member(match, "table").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "users")));
    }

    // A check that fails ends the describe before the match is made.
    [Fact]
    public async Task Describe_CheckThatDiffers_WritesNoTableMatch()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId);

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "column", "check", "deallocate"]);
        Member(log.Events.Single(written => written.Name == "check").Members, "result").ShouldBe((LogValue)"differ");
    }

    // A table's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromTheTableOfATableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), new SessionRunLog(log, "users", "postgres."), Whole);

        var match = log.Events.Single(written => written.Name == "postgres.table-match").Members;
        Member(match, "table").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***")));
    }

    // The same inside a map: the message of an error is a string one level down.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromTheMessageOfAnError()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, ["unknown"], new PostgresPrepareAnswer(null, rejected));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."));

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.prepare");
        var error = Member(members, "error").ShouldBeOfType<LogValue.Members>();
        error
            .Values.Single(value => value.Name == "message")
            .Value.ShouldBe((LogValue)"relation \"***\" does not exist");
    }

    private static readonly DescribeRequest Joined = new(
        "ListUserTotals",
        "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id",
        new EquatableArray<SqlQueryParameter>([])
    );

    private static readonly PostgresColumnRow Total = PostgresTables.ColumnOf(
        1,
        "total",
        PostgresRows.Numeric.Oid,
        PostgresTables.Orders,
        3
    );

    // The plan of Joined: users is the side the join keeps.
    private static readonly string JoinedPlan = Text(
        Join("Left", Scan("u"), Scan("o")) with
        {
            Output = ["u.id", "o.total"],
        }
    );

    // The whole script of Joined, a gated query, with the answer of "explain" the test gives.
    private static PostgresScript JoinedScript(PostgresExplainAnswer explain) =>
        new PostgresScript()
            .Open()
            .Prepares(1, Joined.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, PostgresTables.UsersId, Total)
            .Tables([PostgresTables.Users.Oid, PostgresTables.Orders.Oid], PostgresTables.Users, PostgresTables.Orders)
            .Resolves([23, 1700], PostgresRows.Int4, PostgresRows.Numeric)
            .Checks(Joined.Sql, [], PostgresTables.UsersId, Total)
            .Explains(1, 0, explain)
            .Frees(1);

    [Fact]
    public async Task Describe_GatedQuery_WritesExplainANodeForEachAColumnForEachAndWalkBeforeTheMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "check",
                "explain",
                "walk.node",
                "walk.node",
                "walk.node",
                "walk.column",
                "walk.column",
                "walk",
                "table-match",
                "deallocate",
            ]);

        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "plan", "durationMs"]);
        Member(explain, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(explain, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(explain, "settings").ShouldBe(LogValue.ListOfTexts([]));
        Member(explain, "sql").ShouldBe((LogValue)"EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");
        Member(explain, "plan").ShouldBe((LogValue)JoinedPlan);
        _ = Member(explain, "durationMs").ShouldBeOfType<LogValue.Number>();

        var join = log.Events[8].Members;
        NamesOf(join)
            .ShouldBe(["depth", "nodeType", "joinType", "parentRelationship", "alias", "relation", "nullExtends"]);
        Member(join, "depth").ShouldBe((LogValue)0L);
        Member(join, "nodeType").ShouldBe((LogValue)"Hash Join");
        Member(join, "joinType").ShouldBe((LogValue)"Left");
        Member(join, "parentRelationship").ShouldBe(LogValue.Null);
        Member(join, "alias").ShouldBe(LogValue.Null);
        Member(join, "relation").ShouldBe(LogValue.Null);
        Member(join, "nullExtends").ShouldBe((LogValue)"inner");

        var orders = log.Events[10].Members;
        Member(orders, "depth").ShouldBe((LogValue)1L);
        Member(orders, "nodeType").ShouldBe((LogValue)"Seq Scan");
        Member(orders, "joinType").ShouldBe(LogValue.Null);
        Member(orders, "parentRelationship").ShouldBe((LogValue)"Inner");
        Member(orders, "alias").ShouldBe((LogValue)"o");
        Member(orders, "relation").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "o")));
        Member(orders, "nullExtends").ShouldBe((LogValue)"none");

        var total = log.Events[12].Members;
        NamesOf(total)
            .ShouldBe([
                "ordinal",
                "name",
                "output",
                "alias",
                "column",
                "mapped",
                "nullExtended",
                "nullable",
                "nullableSource",
            ]);
        Member(total, "ordinal").ShouldBe((LogValue)1L);
        Member(total, "name").ShouldBe((LogValue)"total");
        Member(total, "output").ShouldBe((LogValue)"o.total");
        Member(total, "alias").ShouldBe((LogValue)"o");
        Member(total, "column").ShouldBe((LogValue)"total");
        Member(total, "mapped").ShouldBe((LogValue)true);
        Member(total, "nullExtended").ShouldBe((LogValue)true);
        Member(total, "nullable").ShouldBe((LogValue)true);
        Member(total, "nullableSource").ShouldBe((LogValue)"outer-join");

        var walk = log.Events[13].Members;
        NamesOf(walk).ShouldBe(["query", "plan", "reason"]);
        Member(walk, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(walk, "plan").ShouldBe((LogValue)"walked");
        Member(walk, "reason").ShouldBe(LogValue.Null);
    }

    // The column event is written when the column is built, with the heuristic's answer: the walk's own event holds
    // what the sidecar records.
    [Fact]
    public async Task Describe_GatedQuery_KeepsTheHeuristicInTheColumnEventAndTheAnswerInTheWalks()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        var built = log.Events.First(written => written.Name == "column").Members;
        Member(built, "nullableSource").ShouldBe((LogValue)"heuristic");
        Member(built, "gated").ShouldBe((LogValue)true);
        var walked = log.Events.First(written => written.Name == "walk.column").Members;
        Member(walked, "nullable").ShouldBe((LogValue)false);
        Member(walked, "nullableSource").ShouldBe((LogValue)"catalog");
    }

    // The log is where a failed EXPLAIN is told from a bug of the walk.
    [Fact]
    public async Task Describe_ExplainThatFails_WritesItsErrorAndTheReasonAndNoNode()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError(
            "57014",
            "canceling statement due to statement timeout",
            null,
            null,
            null
        );

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(null, refused)), log, Joined);

        log.Events.Select(written => written.Name)
            .Skip(7)
            .ShouldBe(["explain", "walk.column", "walk.column", "walk", "table-match", "deallocate"]);
        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "error", "durationMs"]);
        Member(explain, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "57014"),
                    new("message", "canceling statement due to statement timeout"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
        var column = log.Events[8].Members;
        Member(column, "output").ShouldBe(LogValue.Null);
        Member(column, "mapped").ShouldBe((LogValue)false);
        Member(column, "nullableSource").ShouldBe((LogValue)"heuristic");
        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)"explain-failed");
    }

    [Theory]
    [InlineData("not json", "unreadable")]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Append"}}]""",
        "no-output"
    )]
    public async Task Describe_PlanThatCannotBeUsed_WritesItsReason(string plan, string reason)
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(plan, null)), log, Joined);

        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)reason);
    }

    // A plan holds a user's names, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromThePlanAndFromANode()
    {
        var log = new RecordingLog();

        await DescribeAsync(
            JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)),
            new SessionRunLog(log, "Seq Scan", "postgres."),
            Joined
        );

        var plan = Member(log.Events.Single(written => written.Name == "postgres.explain").Members, "plan");
        plan.ShouldBeOfType<LogValue.Text>().Value.ShouldNotContain("Seq Scan");
        var scan = log.Events.Last(written => written.Name == "postgres.walk.node").Members;
        Member(scan, "nodeType").ShouldBe((LogValue)"***");
    }
}

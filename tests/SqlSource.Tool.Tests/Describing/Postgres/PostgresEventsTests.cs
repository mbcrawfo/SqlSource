using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says of each call in the run's log: what it asked and what the server answered.
public class PostgresEventsTests
{
    private static readonly DescribeRequest Query = new(
        "GetUser",
        "SELECT 1 FROM users WHERE id = @id",
        new EquatableArray<SqlQueryParameter>([new SqlQueryParameter("id", null, null, false)])
    );

    private static readonly PostgresPrepareRequest Prepare = new(
        "sqlsource_1",
        "SELECT 1 FROM users WHERE id = $1",
        ["unknown"]
    );

    private static readonly PostgresDeallocateRequest Deallocate = new("sqlsource_1");

    private static readonly PostgresColumnsRequest Columns = new("sqlsource_1", 1);

    private static readonly PostgresCheckRequest Check = new(
        "SELECT 1 FROM users WHERE id = @id",
        [new PostgresCheckParameter("id", "integer")]
    );

    private static readonly PostgresCheckAnswer Agrees = new([], null, null);

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
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, Agrees)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "check", "deallocate"]);

        var prepare = log.Events[0].Members;
        NamesOf(prepare).ShouldBe(["query", "statement", "sql", "types", "parameterTypes", "durationMs"]);
        Member(prepare, "query").ShouldBe((LogValue)"GetUser");
        Member(prepare, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(prepare, "sql").ShouldBe((LogValue)"SELECT 1 FROM users WHERE id = $1");
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
                        new("enumLabels", LogValue.Null)
                    ),
                ])
            );

        var deallocate = log.Events[4].Members;
        NamesOf(deallocate).ShouldBe(["statement", "error", "durationMs"]);
        Member(deallocate, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(deallocate, "error").ShouldBe(LogValue.Null);
    }

    // A prepare that failed has its error in the place of the types, and nothing else is asked or written.
    [Fact]
    public async Task Describe_PrepareThatFails_WritesItsErrorAndNoOtherEvent()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", null, 49);
        var script = new PostgresScript().Open().Answer("prepare", Prepare, new PostgresPrepareAnswer(null, rejected));

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
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, refused))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

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
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, Agrees)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(gone));

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
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, Agrees)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

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

    private static readonly PostgresColumnRow IdColumn = PostgresTables.ColumnOf(0, "id", 23, PostgresTables.Users, 1);

    private static readonly PostgresColumnRow CountColumn = new(1, "n", 20, 0, 0, null, null, null);

    // The script of Listing up to "types", which a test ends as it needs.
    private static PostgresScript ListingScript() =>
        new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer([IdColumn, CountColumn], null)
            )
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid]),
                new PostgresTablesAnswer([PostgresTables.Users], null)
            )
            .Answer(
                "types",
                new PostgresTypesRequest([20, 23]),
                new PostgresTypesAnswer([PostgresRows.Int8, PostgresRows.Int4], null)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

    private static PostgresCheckAnswer Sees(params PostgresColumnRow[] columns) =>
        new(
            [.. columns.Select(static column => new PostgresCheckColumn(column.Name, column.TypeOid, "a type"))],
            null,
            null
        );

    [Fact]
    public async Task Describe_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck()
    {
        var log = new RecordingLog();
        var script = ListingScript()
            .Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn, CountColumn));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "table-match",
                "check",
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
                    new("scale", LogValue.Null)
                )
            );

        var tables = log.Events[2].Members;
        NamesOf(tables).ShouldBe(["oids", "tables", "durationMs"]);
        Member(tables, "oids").ShouldBe(LogValue.ListOf([PostgresTables.Users.Oid]));
        var users = Member(tables, "tables")
            .ShouldBeOfType<LogValue.Items>()
            .Values[0]
            .ShouldBeOfType<LogValue.Members>();
        users.Values.Select(value => value.Name).ShouldBe(["oid", "relkind", "schema", "name", "columns"]);
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

        var match = log.Events[6].Members;
        NamesOf(match).ShouldBe(["query", "result", "table"]);
        Member(match, "query").ShouldBe((LogValue)"ListUsers");
        Member(match, "result").ShouldBe((LogValue)"no-origin");
        Member(match, "table").ShouldBe(LogValue.Null);

        var check = log.Events[7].Members;
        NamesOf(check).ShouldBe(["query", "parameters", "result", "differs", "durationMs"]);
        Member(check, "result").ShouldBe((LogValue)"same");
        Member(check, "differs").ShouldBe(LogValue.Null);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesItsNameAndTypeNameInCheck()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, Agrees)
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        Member(log.Events.Single(written => written.Name == "check").Members, "parameters")
            .ShouldBe(LogValue.ListOf([LogValue.MapOf(new("name", "id"), new("dataTypeName", "integer"))]));
    }

    [Fact]
    public async Task Describe_CheckThatDiffers_WritesWhatDiffers()
    {
        var log = new RecordingLog();
        var script = ListingScript().Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn));

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
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, PostgresScript.NoColumns)
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", Check, new PostgresCheckAnswer(null, null, new PostgresBindError("id", "no such type")))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"unbound");
        Member(check, "differs").ShouldBe((LogValue)"no such type");
    }

    // A columns call that failed has its error in the place of the columns.
    [Fact]
    public async Task Describe_ColumnsThatFails_WritesItsErrorInThePlaceOfTheColumns()
    {
        var log = new RecordingLog();
        var lost = new PostgresServerError(null, "the connection to the server was lost", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("columns", Columns, new PostgresColumnsAnswer(null, lost))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

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
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer([IdColumn], null)
            )
            .Answer(
                "tables",
                new PostgresTablesRequest([PostgresTables.Users.Oid]),
                new PostgresTablesAnswer(null, refused)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

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
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Listing.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer(
                "columns",
                new PostgresColumnsRequest("sqlsource_1", 0),
                new PostgresColumnsAnswer(
                    [
                        new(0, "one", 23, 0, 0, null, null, null),
                        new(1, "pair", 2249, 0, 0, null, null, null),
                        new(2, "two", 23, 0, 0, null, null, null),
                    ],
                    null
                )
            )
            .Answer(
                "types",
                new PostgresTypesRequest([23, 2249]),
                new PostgresTypesAnswer([PostgresRows.Int4, PostgresRows.Record], null)
            )
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "column", "deallocate"]);
        Member(log.Events[3].Members, "name").ShouldBe((LogValue)"one");
    }

    // A column's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin()
    {
        var log = new RecordingLog();
        var script = ListingScript()
            .Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn, CountColumn));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."), Listing);

        var id = log.Events.First(written => written.Name == "postgres.column").Members;
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***"), new("column", "id")));
    }

    // A table of one column, whole in the answer of "tables", and the query that is the table.
    private static readonly PostgresTableRow Ids = PostgresTables.TableOf(
        16500,
        "users",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true)
    );

    private static readonly DescribeRequest Whole = new(
        "ListIds",
        "SELECT * FROM users",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static PostgresScript WholeScript()
    {
        var id = PostgresTables.ColumnOf(0, "id", 23, Ids, 1);
        return new PostgresScript()
            .Open()
            .Answer(
                "prepare",
                new PostgresPrepareRequest("sqlsource_1", Whole.Sql, []),
                new PostgresPrepareAnswer([], null)
            )
            .Answer("columns", new PostgresColumnsRequest("sqlsource_1", 0), new PostgresColumnsAnswer([id], null))
            .Answer("tables", new PostgresTablesRequest([Ids.Oid]), new PostgresTablesAnswer([Ids], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("check", new PostgresCheckRequest(Whole.Sql, []), Sees(id))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));
    }

    [Fact]
    public async Task Describe_QueryThatIsATable_WritesTheTableInTableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), log, Whole);

        log.Events.Select(written => written.Name)
            .ShouldBe(["prepare", "columns", "tables", "types", "column", "table-match", "check", "deallocate"]);
        var match = log.Events[5].Members;
        Member(match, "query").ShouldBe((LogValue)"ListIds");
        Member(match, "result").ShouldBe((LogValue)"matched");
        Member(match, "table").ShouldBe((LogValue)"public.users");
    }

    // The match is in the log when the check that follows it fails.
    [Fact]
    public async Task Describe_CheckThatDiffers_StillWritesTheTableMatchBeforeIt()
    {
        var log = new RecordingLog();
        var script = ListingScript().Answer("check", new PostgresCheckRequest(Listing.Sql, []), Sees(IdColumn));

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "table-match",
                "check",
                "deallocate",
            ]);
    }

    // A table's name is a user's text, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromTheTableOfATableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), new SessionRunLog(log, "users", "postgres."), Whole);

        var match = log.Events.Single(written => written.Name == "postgres.table-match").Members;
        Member(match, "table").ShouldBe((LogValue)"public.***");
    }

    // The same inside a map: the message of an error is a string one level down.
    [Fact]
    public async Task Describe_ThroughTheLogOfASession_RemovesTheSecretFromTheMessageOfAnError()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", null, null, null);
        var script = new PostgresScript().Open().Answer("prepare", Prepare, new PostgresPrepareAnswer(null, rejected));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."));

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.prepare");
        var error = Member(members, "error").ShouldBeOfType<LogValue.Members>();
        error
            .Values.Single(value => value.Name == "message")
            .Value.ShouldBe((LogValue)"relation \"***\" does not exist");
    }
}

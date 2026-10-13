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

    private static LogValue Member(LogMember[] members, string name) =>
        members.Single(member => member.Name == name).Value;

    private static string[] NamesOf(LogMember[] members) => [.. members.Select(member => member.Name)];

    private static async Task DescribeAsync(PostgresScript script, IRunLog log)
    {
        await using var session = await PostgresScript.SessionAsync(script.Play(), log);
        _ = await session.DescribeAsync(Query, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Describe_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Answer("prepare", Prepare, new PostgresPrepareAnswer([23], null))
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "types", "deallocate"]);

        var prepare = log.Events[0].Members;
        NamesOf(prepare).ShouldBe(["query", "statement", "sql", "types", "parameterTypes", "durationMs"]);
        Member(prepare, "query").ShouldBe((LogValue)"GetUser");
        Member(prepare, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(prepare, "sql").ShouldBe((LogValue)"SELECT 1 FROM users WHERE id = $1");
        Member(prepare, "types").ShouldBe(LogValue.ListOfTexts(["unknown"]));
        Member(prepare, "parameterTypes").ShouldBe(LogValue.ListOf([23L]));
        _ = Member(prepare, "durationMs").ShouldBeOfType<LogValue.Number>();

        var types = log.Events[1].Members;
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

        var deallocate = log.Events[2].Members;
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
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer(null, refused))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "types", "deallocate"]);
        var types = log.Events[1].Members;
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
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(gone));

        await DescribeAsync(script, log);

        var error = Member(log.Events[2].Members, "error").ShouldBeOfType<LogValue.Members>();
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
            .Answer("types", new PostgresTypesRequest([23]), new PostgresTypesAnswer([PostgresRows.Int4], null))
            .Answer("deallocate", Deallocate, new PostgresDeallocateAnswer(null));

        await DescribeAsync(script, new SessionRunLog(log, "users", "postgres."));

        log.Events.Select(written => written.Name)
            .ShouldBe(["postgres.prepare", "postgres.types", "postgres.deallocate"]);
        Member(log.Events[0].Members, "sql").ShouldBe((LogValue)"SELECT 1 FROM *** WHERE id = $1");
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

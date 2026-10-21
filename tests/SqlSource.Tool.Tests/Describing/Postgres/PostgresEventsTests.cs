using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using Xunit;
using static SqlSource.Tool.Tests.PostgresEventCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of prepare, of the types and their spellings, and of deallocate.
public sealed class PostgresEventsTests
{
    [Fact]
    public async Task DescribeAsync_QueryWithAParameter_WritesPrepareTypesAndDeallocateInOrder()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks(Query.Sql, CheckedParameters)
            .Frees(1);

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "types", "check", "deallocate"]);

        // Each call took the 7 ms that the exchange moved the clock by.
        foreach (var (_, members) in log.Events)
        {
            Member(members, "durationMs").ShouldBe((LogValue)7L);
        }

        var prepare = log.Events[0].Members;
        NamesOf(prepare).ShouldBe(["query", "statement", "sql", "types", "parameterTypes", "durationMs"]);
        Member(prepare, "query").ShouldBe((LogValue)"GetUser");
        Member(prepare, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(prepare, "sql").ShouldBe((LogValue)"SELECT 1 FROM users WHERE id = $1");
        Member(prepare, "types").ShouldBe(LogValue.ListOfTexts(["unknown"]));
        Member(prepare, "parameterTypes").ShouldBe(LogValue.ListOf([23L]));

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
    public async Task DescribeAsync_PrepareThatFails_WritesItsErrorAndNoOtherEvent()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", null, 49);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer(null, rejected));

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
    public async Task DescribeAsync_TypesThatFails_WritesItsErrorInThePlaceOfTheRows()
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
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
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
    public async Task DescribeAsync_DeallocateThatFails_WritesTheErrorThatIsNeverReported()
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
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks(Query.Sql, CheckedParameters)
            .Frees(1, new PostgresDeallocateAnswer(gone));

        await DescribeAsync(script, log);

        var error = Member(log.Events[^1].Members, "error").ShouldBeOfType<LogValue.Members>();
        error.Values[0].ShouldBe(new LogMember("sqlState", "26000"));
    }

    // As a run gives the describer its log: the engine before each name, and the connection's value written as ***
    // wherever a describer put it, the SQL of a query included.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_HasTheEngineBeforeEachNameAndNothingOfTheSecret()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
            .Columns(1, 1)
            .Resolves([23], PostgresRows.Int4)
            .Checks(Query.Sql, CheckedParameters)
            .Frees(1);

        await DescribeAsync(script, new SessionRunLog(log, new ConnectionSecret("users"), "postgres."));

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

    // The same inside a map: the message of an error is a string one level down.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheMessageOfAnError()
    {
        var log = new RecordingLog();
        var rejected = new PostgresServerError("42P01", "relation \"users\" does not exist", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer(null, rejected));

        await DescribeAsync(script, new SessionRunLog(log, new ConnectionSecret("users"), "postgres."));

        var (name, members) = log.Events.ShouldHaveSingleItem();
        name.ShouldBe("postgres.prepare");
        var error = Member(members, "error").ShouldBeOfType<LogValue.Members>();
        error
            .Values.Single(value => value.Name == "message")
            .Value.ShouldBe((LogValue)"relation \"***\" does not exist");
    }
}

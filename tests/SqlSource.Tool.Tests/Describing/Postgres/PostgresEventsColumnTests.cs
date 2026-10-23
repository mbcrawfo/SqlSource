using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.PostgresEventCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of the columns, the tables and the check.
public sealed class PostgresEventsColumnTests
{
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

        PostgresEvents.Columns(log, "GetUser", ColumnsOfQuery, answer, 1);

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

    private static readonly PostgresCheckRequest Check = new(
        Query.Sql,
        [.. CheckedParameters.Select(static parameter => new PostgresCheckParameter(parameter.Name, parameter.Type))]
    );

    [Fact]
    public async Task DescribeAsync_QueryWithColumns_WritesColumnsTablesAColumnForEachAndCheck()
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
        NamesOf(tables).ShouldBe(["query", "oids", "tables", "durationMs"]);
        Member(tables, "query").ShouldBe((LogValue)"ListUsers");
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
    public async Task DescribeAsync_QueryWithAParameter_WritesItsNameAndTypeNameInCheck()
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

        Member(log.Events.Single(written => written.Name == "check").Members, "parameters")
            .ShouldBe(LogValue.ListOf([LogValue.MapOf(new("name", "id"), new("dataTypeName", "integer"))]));
    }

    [Fact]
    public async Task DescribeAsync_CheckThatDiffers_WritesWhatDiffers()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId);

        await DescribeAsync(script, log, Listing);

        var check = log.Events.Single(written => written.Name == "check").Members;
        Member(check, "result").ShouldBe((LogValue)"differ");
        Member(check, "differs").ShouldBe((LogValue)"the result has 1 columns for Npgsql and 2 for SqlSource");
    }

    [Fact]
    public async Task DescribeAsync_CheckWithATypeThatCannotBeBound_SaysUnboundWithTheDriversMessage()
    {
        var log = new RecordingLog();
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
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
    public async Task DescribeAsync_CheckThatTheServerCancels_SaysErrorWithItsMessage()
    {
        var log = new RecordingLog();
        var canceled = new PostgresServerError("57014", "canceling statement due to user request", null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
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
    public async Task DescribeAsync_ColumnsThatFails_WritesItsErrorInThePlaceOfTheColumns()
    {
        var log = new RecordingLog();
        var lost = new PostgresServerError(null, PostgresConnection.LostConnection, null, null, null);
        var script = new PostgresScript()
            .Open()
            .Prepares(1, PreparedSql, PreparedTypes, new PostgresPrepareAnswer([23], null))
            .Answer("columns", ColumnsOfQuery, new PostgresColumnsAnswer(null, lost))
            .Frees(1);

        await DescribeAsync(script, log);

        log.Events.Select(written => written.Name).ShouldBe(["prepare", "columns", "deallocate"]);
        NamesOf(log.Events[1].Members).ShouldBe(["query", "statement", "error", "durationMs"]);
    }

    [Fact]
    public async Task DescribeAsync_TablesThatFails_WritesItsErrorInThePlaceOfTheTables()
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
        NamesOf(log.Events[2].Members).ShouldBe(["query", "oids", "error", "durationMs"]);
    }

    // The describe ends at the first column of a pseudo-type: the columns before it have their events, and it and
    // the ones after it have none.
    [Fact]
    public async Task DescribeAsync_ColumnOfAPseudoType_WritesAColumnForTheColumnsBeforeItAlone()
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
    public async Task DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromAColumnsNameAndItsOrigin()
    {
        var log = new RecordingLog();
        var script = ListingScript().Checks(Listing.Sql, [], PostgresTables.UsersId, CountColumn).PlansNothing(1, 0);

        await DescribeAsync(script, new SessionRunLog(log, new ConnectionSecret("users"), "postgres."), Listing);

        var id = log.Events.First(written => written.Name == "postgres.column").Members;
        Member(id, "origin")
            .ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***"), new("column", "id")));
    }
}

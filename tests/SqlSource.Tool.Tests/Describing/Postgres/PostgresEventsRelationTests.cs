using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.PostgresEventCases;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of the partitions, the relations and the table match.
public sealed class PostgresEventsRelationTests
{
    // A plan over an Append of two partitions asks their ancestors between "explain" and the walk, and the event says
    // what was asked and what was answered.
    [Fact]
    public async Task DescribeAsync_PlanWithAnAppendOfPartitions_WritesPartitionsBeforeTheWalk()
    {
        var log = new RecordingLog();
        var plan = Over(
            "WindowAgg",
            Append(Member("u_1", "users_1", "u_1.id"), Member("u_2", "users_2", "u_2.id"))
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
            )
            .Relations(asked.Relations);

        await DescribeAsync(script, log, Listing);

        log.Events.Select(written => written.Name)
            .SkipWhile(static name => name != "explain")
            .Take(4)
            .ShouldBe(["explain", "partitions", "relations", "walk.node"]);
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
    public async Task DescribeAsync_PartitionsThatFails_WritesItsErrorInThePlaceOfTheRows()
    {
        var log = new RecordingLog();
        var plan = Append(Member("u_1", "users_1", "u_1.id")) with { Output = ["u.id", "1"] };
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
    public async Task DescribeAsync_QueryThatIsATable_WritesTheTableInTableMatch()
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
    public async Task DescribeAsync_CheckThatDiffers_WritesNoTableMatch()
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
    public async Task DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromTheTableOfATableMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(WholeScript(), new SessionRunLog(log, new ConnectionSecret("users"), "postgres."), Whole);

        var match = log.Events.Single(written => written.Name == "postgres.table-match").Members;
        Member(match, "table").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "***")));
    }
}

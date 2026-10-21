using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresSessionCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the partitions of a plan, the relations of a set operation, and the table
// match.  A call the script does not hold is a CaptureMissException, so a describe that asks more than its test wrote
// fails.
public sealed class PostgresSessionRelationTests
{
    // users scanned as the partitions users_1 and users_2, on the side the join keeps.
    private static readonly PlanNode PartitionedUsers = Over(
        "WindowAgg",
        Join(
            "Left",
            Append(Member("u_1", "users_1", "u_1.id"), Member("u_2", "users_2", "u_2.id")),
            Scan("o", "o.user_id")
        ) with
        {
            Output = ["u.id"],
        }
    ) with
    {
        Output = ["u.id", "count(*) OVER w1"],
    };

    private static readonly PostgresPartitionsRequest PartitionsOfUsers = new([
        new PostgresRelationName("public", "users_1"),
        new PostgresRelationName("public", "users_2"),
    ]);

    // The query of users.id and an aggregate, planned over the partitions, with "partitions" answering what the test
    // gives.
    private static async Task<QueryDescription> DescribeUsersIdOverPartitionsAsync(PostgresPartitionsAnswer partitions)
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            GatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(PartitionedUsers));
        _ = script.Partitions(PartitionsOfUsers, partitions);
        _ = script.Relations([new PostgresRelationName("public", "o"), .. PartitionsOfUsers.Relations]);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(GatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // A plan with an Append of scans under one alias and a number asks the ancestors of their relations, and the walk
    // reads the Append as the table they are partitions of.
    [Fact]
    public async Task DescribeAsync_PlanWithAnAppendOfPartitions_AsksTheirAncestorsAndWalksThroughThem()
    {
        var users = PostgresTables.Users;

        var description = await DescribeUsersIdOverPartitionsAsync(
            new PostgresPartitionsAnswer(
                [new("public", "users_1", [users.Oid]), new("public", "users_2", [users.Oid])],
                null
            )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    // A "partitions" that fails is logged and never reported: the Append is read as a set operation.
    [Fact]
    public async Task DescribeAsync_PartitionsThatFails_ReadsTheAppendAsASetOperation()
    {
        var description = await DescribeUsersIdOverPartitionsAsync(
            new PostgresPartitionsAnswer(null, new PostgresServerError("XX000", "no catalog", null, null, null))
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NullableHeuristic, UnknownNoOrigin]);
    }

    // A UNION ALL of users and of orders, or of orders' partitions, whose first column has users.id as its origin in
    // the row description the test writes, and whose second has none: the plan answers it from its branches.
    private const string UnionOfUsersAndOrders =
        "SELECT id, user_id FROM users UNION ALL SELECT user_id, user_id FROM o";

    private static PlanNode UnionPlan(bool partitioned)
    {
        var orders = partitioned
            ? Append(
                Member("o_1", "orders_1", "o_1.user_id", "o_1.user_id"),
                Member("o_2", "orders_2", "o_2.user_id", "o_2.user_id")
            )
            : Scan("orders", "orders.user_id", "orders.user_id");
        return Append(Member(Scan("users", "users.id", "users.id")), Member(orders));
    }

    private static readonly PostgresTableRow Orders1 = PostgresTables.Orders with { Oid = 16420, Name = "orders_1" };

    private static readonly PostgresTableRow Orders2 = PostgresTables.Orders with { Oid = 16421, Name = "orders_2" };

    // The describe of the UNION ALL, with each call after "explain" the script writes.
    private static async Task<(QueryDescription Description, RecordingLog Log)> DescribeUnionAsync(
        PlanNode plan,
        Action<PostgresScript> afterExplain
    )
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, UnionOfUsersAndOrders, [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.UsersId,
            PostgresTables.ExpressionOf(1, "user_id", PostgresRows.Int4.Oid)
        );
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(
            UnionOfUsersAndOrders,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "user_id", PostgresRows.Int4.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        afterExplain(script);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(
            Listed(UnionOfUsersAndOrders),
            TestContext.Current.CancellationToken
        );

        exchange.Unused.ShouldBeEmpty();
        return (described.Value.ShouldNotBeNull(), log);
    }

    // "relations" is asked after "partitions", for the relations that "tables" did not answer, and the walk reads
    // their NOT NULL.
    [Fact]
    public async Task DescribeAsync_SetOperationWithAColumnOfNoOrigin_AsksTheRelationsItsBranchesScan()
    {
        var (description, log) = await DescribeUnionAsync(
            UnionPlan(partitioned: true),
            script =>
            {
                _ = script.Answer(
                    "partitions",
                    new PostgresPartitionsRequest([
                        new PostgresRelationName("public", "orders_1"),
                        new PostgresRelationName("public", "orders_2"),
                    ]),
                    new PostgresPartitionsAnswer(
                        [
                            new("public", "orders_1", [PostgresTables.Orders.Oid]),
                            new("public", "orders_2", [PostgresTables.Orders.Oid]),
                        ],
                        null
                    )
                );
                _ = script.Answer(
                    "relations",
                    new PostgresRelationsRequest([
                        new PostgresRelationName("public", "orders_1"),
                        new PostgresRelationName("public", "orders_2"),
                    ]),
                    new PostgresTablesAnswer([Orders1, Orders2], null)
                );
            }
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullSetOperation]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "explain" or "partitions" or "relations" or "walk")
            .ShouldBe(["explain", "partitions", "relations", "walk"]);
    }

    // Every relation the plan scans is one "tables" answered: nothing more is asked, and the script holds nothing.
    [Fact]
    public async Task DescribeAsync_SetOperationOfRelationsTablesAnswered_AsksNoRelations()
    {
        var plan = Append(Member(Scan("users", "users.id", "users.id")), Member("u2", "users", "u2.id", "u2.id"));

        var (description, _) = await DescribeUnionAsync(plan, static _ => { });

        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullSetOperation]);
    }

    // The root reaches no set operation: the one under the join is not taken on.  The relation "tables" did not answer
    // is asked all the same, since a column has no origin.
    [Fact]
    public async Task DescribeAsync_SetOperationUnderAJoin_AsksRelationsAndKeepsTheColumn()
    {
        var plan = Join("Left", Scan("users", "users.id"), UnionPlan(partitioned: false)) with
        {
            Output = ["users.id", "orders.user_id"],
        };

        var (description, _) = await DescribeUnionAsync(
            plan,
            static script => script.Relations([new PostgresRelationName("public", "orders")], PostgresTables.Orders)
        );

        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A gated query whose every column has an origin: the plan answers no column of no origin, and nothing more is
    // asked.
    [Fact]
    public async Task DescribeAsync_SetOperationWhoseEveryColumnHasAnOrigin_AsksNoRelations()
    {
        const string Sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id UNION ALL SELECT 1";
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(Sql, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        _ = script.Explains(1, 0, PostgresScript.PlanOf(UnionPlan(partitioned: false)));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NullableHeuristic]);
    }

    // Orders with a placed_at that is never NULL, which "tables" does not answer for a query that returns none of its
    // columns.
    private static readonly PostgresTableRow PlacedOrders = PostgresTables.TableOf(
        16415,
        "orders",
        PostgresTables.Attribute(2, "user_id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(3, "placed_at", PostgresRows.Timestamptz.Oid, notNull: true)
    );

    private const string LastPlacedAt =
        "SELECT u.id, max(o.placed_at) AS last_placed_at FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id";

    // The plain columns are all of users and the expression names orders, which "tables" did not answer: with no set
    // operation in the plan, "relations" is asked for it after "explain", and max(o.placed_at) of a group is proved.
    [Fact]
    public async Task DescribeAsync_ExpressionOverARelationNoColumnNames_AsksRelations()
    {
        var users = PostgresTables.Users;
        var plan = Aggregate(
            "Hashed",
            true,
            Join("Inner", Scan("users", "users.id"), Scan("orders", "orders.placed_at")) with
            {
                Output = ["users.id", "orders.placed_at"],
            },
            "users.id",
            "max(orders.placed_at)"
        ) with
        {
            Output = ["users.id", "max(orders.placed_at)"],
        };
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, LastPlacedAt, [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.UsersId,
            PostgresTables.ExpressionOf(1, "last_placed_at", PostgresRows.Timestamptz.Oid)
        );
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23, 1184], PostgresRows.Int4, PostgresRows.Timestamptz);
        _ = script.Checks(
            LastPlacedAt,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "last_placed_at", PostgresRows.Timestamptz.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        _ = script.Relations([new PostgresRelationName("public", "orders")], PlacedOrders);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(Listed(LastPlacedAt), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        var description = described.Value.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "explain" or "relations" or "walk")
            .ShouldBe(["explain", "relations", "walk"]);
    }

    // A "relations" that fails is logged and never reported: the walk goes on without its rows.
    [Fact]
    public async Task DescribeAsync_RelationsThatFails_IsLoggedAndTheColumnIsKept()
    {
        var (description, log) = await DescribeUnionAsync(
            UnionPlan(partitioned: false),
            static script =>
                script.Answer(
                    "relations",
                    new PostgresRelationsRequest([new PostgresRelationName("public", "orders")]),
                    new PostgresTablesAnswer(null, new PostgresServerError("XX000", "no catalog", null, null, null))
                )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
        var (_, members) = log.Events.Single(written => written.Name == "relations");
        members.ShouldContain(member => member.Name == "error");
    }

    // A table of two columns, whole in the answer of "tables".
    private static readonly PostgresTableRow Labels = PostgresTables.TableOf(
        16500,
        "labels",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "label", PostgresRows.Text.Oid)
    );

    // One script for a query whose columns are the two of labels in their order, the second under the name given.
    private static async Task<QueryDescription> DescribeLabelsAsync(
        string sql,
        string second = "label",
        PostgresExplainAnswer? explain = null
    )
    {
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, sql, [], new([], null));
        _ = script.Columns(
            1,
            0,
            PostgresTables.ColumnOf(0, "id", PostgresRows.Int4.Oid, Labels, 1),
            PostgresTables.ColumnOf(1, second, 25, Labels, 2)
        );
        _ = script.Tables([Labels.Oid], Labels);
        _ = script.Resolves([23, 25], PostgresRows.Int4, PostgresRows.Text);
        _ = script.Checks(
            sql,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, second, 25)
        );
        if (explain is not null)
        {
            _ = script.Explains(1, 0, explain);
        }

        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // The match asks nothing: the script holds the calls of any query with columns, and no other.
    [Fact]
    public async Task DescribeAsync_EveryColumnOfATableInItsOrder_SaysTheTable()
    {
        var description = await DescribeLabelsAsync("SELECT * FROM labels");

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
    }

    // The name is the one the server returned.
    [Fact]
    public async Task DescribeAsync_ColumnUnderAnotherName_MatchesNothingAndSaysNamesDiffer()
    {
        var description = await DescribeLabelsAsync("SELECT id, label AS \"label!\" FROM labels", "label!");

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NamesDiffer);
    }

    // The match is made after the walk and reads the nullability it set: labels is the side the join extends, so
    // its NOT NULL column is nullable and the result is not the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatExtendsATablesList_MatchesNothingAndSaysNullabilityDiffers()
    {
        var plan = Join("Left", Scan("o"), Scan("l")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM orders o LEFT JOIN labels l ON l.id = o.id",
            explain: PostgresScript.PlanOf(plan)
        );

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // The other way round, labels keeps every row as it is, and the result is the table's.
    [Fact]
    public async Task DescribeAsync_GatedQueryThatKeepsATablesList_MatchesTheTable()
    {
        var plan = Join("Left", Scan("l"), Scan("o")) with { Output = ["l.id", "l.label"] };

        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: PostgresScript.PlanOf(plan)
        );

        description.MatchesTable.ShouldBe(new SidecarTable("public", "labels"));
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // Without a plan the heuristic stands, and the match reads that.
    [Fact]
    public async Task DescribeAsync_GatedQueryOverATablesListWithoutAPlan_SaysNullabilityDiffers()
    {
        var description = await DescribeLabelsAsync(
            "SELECT l.* FROM labels l LEFT JOIN orders o ON o.id = l.id",
            explain: new PostgresExplainAnswer(null, new PostgresServerError(null, "lost", null, null, null))
        );

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NullabilityDiffers);
        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
    }
}

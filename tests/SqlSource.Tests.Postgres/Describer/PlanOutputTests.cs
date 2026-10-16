using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// What PostgreSQL 14 and 18 print in a plan's Output for each form PlanExpression reads, and for the texts near
// them: a form is trusted only where a server is seen to print it so.  A row that fails on a server is a form the
// recogniser must not read as written.
[Collection(UsesPostgres.Name)]
public sealed class PlanOutputTests(PostgresContainer postgres)
{
    private const string OneTable =
        "SELECT deleted_at IS NULL, greatest(created_at, deleted_at), coalesce(email, 'x') FROM users a ORDER BY id";

    private const string Windows =
        "SELECT id, count(*) FILTER (WHERE status = 'active') AS a, count(DISTINCT name) AS d, count(*) OVER () AS w, "
        + "row_number() OVER (ORDER BY id) AS r, rank() OVER w2, dense_rank() OVER w2, percent_rank() OVER w2, "
        + "cume_dist() OVER w2 FROM users GROUP BY id WINDOW w2 AS (ORDER BY id)";

    private const string Constants =
        "SELECT count(*), coalesce(max(email::text), 'x'), 'lit' AS l, NULL::text AS n, 1, 1.5, -1, -1.5, true, "
        + "5000000000, 1.5::float8 FROM users";

    private const string Tests =
        "SELECT u.deleted_at IS NULL, u.deleted_at IS NOT NULL, u.email IS DISTINCT FROM u.name, "
        + "u.email IS NOT DISTINCT FROM u.name, CASE WHEN u.deleted_at IS NULL THEN 'open' ELSE 'closed' END, "
        + "CASE u.status WHEN 'active' THEN 'A' END, greatest(u.created_at, u.deleted_at), least(1, u.id), "
        + "u.email::text, upper(u.name), 'x'::email_t FROM users u JOIN orders o ON o.user_id = u.id";

    // The root of the plan of the SQL, on a connection of the test's own.
    private async Task<PlanNode> ExplainAsync(string sql, string? connection = null) =>
        PlanNode.Read(await ExplainTextAsync(sql, connection)).ShouldNotBeNull();

    // The plan of the SQL as the server writes it, for a key that PlanNode does not read.
    private async Task<string> ExplainTextAsync(string sql, string? connection = null)
    {
        await using var source = NpgsqlDataSource.Create(connection ?? postgres.ConnectionString);
        await using var command = source.CreateCommand("EXPLAIN (VERBOSE, FORMAT JSON) " + sql);
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    // The generic plan of a statement of one parameter, with a NULL for it, as the describer asks for it, and with
    // partition pruning or without.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL and the type are the test's own constants."
    )]
    private async Task<PlanNode> ExplainExecuteAsync(string sql, string type, bool pruning)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        foreach (
            var text in new[]
            {
                "SET LOCAL plan_cache_mode = force_generic_plan",
                $"SET LOCAL enable_partition_pruning = {(pruning ? "on" : "off")}",
                $"PREPARE plan_output ({type}) AS {sql}",
            }
        )
        {
            await using var set = new NpgsqlCommand(text, connection, transaction);
            _ = await set.ExecuteNonQueryAsync(token);
        }

        await using var command = new NpgsqlCommand(
            "EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE plan_output(NULL)",
            connection,
            transaction
        );
        var plan = (string)(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
        await transaction.RollbackAsync(token);
        await using var deallocate = new NpgsqlCommand("DEALLOCATE plan_output", connection);
        _ = await deallocate.ExecuteNonQueryAsync(token);
        return PlanNode.Read(plan).ShouldNotBeNull();
    }

    private async Task RunAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(Constants, 0, "count(*)", "count(*)", false)]
    [InlineData(
        Constants,
        1,
        "COALESCE(max((email)::text), 'x'::text)",
        "COALESCE(max((email)::text), 'x'::text)",
        false
    )]
    [InlineData(Constants, 2, "'lit'::text", "'lit'::text", false)]
    [InlineData(Constants, 3, "NULL::text", "NULL::text", true)]
    [InlineData(Constants, 4, "1", "1", false)]
    [InlineData(Constants, 5, "1.5", "1.5", false)]
    [InlineData(Constants, 6, "'-1'::integer", "'-1'::integer", false)]
    [InlineData(Constants, 7, "'-1.5'::numeric", "'-1.5'::numeric", false)]
    [InlineData(Constants, 8, "true", "true", false)]
    [InlineData(Constants, 9, "'5000000000'::bigint", "'5000000000'::bigint", false)]
    [InlineData(Constants, 10, "'1.5'::double precision", "'1.5'::double precision", false)]
    [InlineData(
        Windows,
        1,
        "(count(*) FILTER (WHERE (status = 'active'::user_status)))",
        "(count(*) FILTER (WHERE (status = 'active'::user_status)))",
        false
    )]
    [InlineData(Windows, 2, "(count(DISTINCT name))", "(count(DISTINCT name))", null)]
    [InlineData(Windows, 3, "count(*) OVER (?)", "count(*) OVER w1", false)]
    [InlineData(Windows, 4, "(row_number() OVER (?))", "(row_number() OVER w2)", false)]
    [InlineData(Windows, 5, "(rank() OVER (?))", "(rank() OVER w2)", false)]
    [InlineData(Windows, 6, "(dense_rank() OVER (?))", "(dense_rank() OVER w2)", false)]
    [InlineData(Windows, 7, "(percent_rank() OVER (?))", "(percent_rank() OVER w2)", false)]
    [InlineData(Windows, 8, "(cume_dist() OVER (?))", "(cume_dist() OVER w2)", false)]
    [InlineData(Tests, 0, "(u.deleted_at IS NULL)", "(u.deleted_at IS NULL)", false)]
    [InlineData(Tests, 1, "(u.deleted_at IS NOT NULL)", "(u.deleted_at IS NOT NULL)", false)]
    [InlineData(
        Tests,
        2,
        "((u.email)::text IS DISTINCT FROM (u.name)::text)",
        "((u.email)::text IS DISTINCT FROM (u.name)::text)",
        false
    )]
    [InlineData(
        Tests,
        3,
        "(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))",
        "(NOT ((u.email)::text IS DISTINCT FROM (u.name)::text))",
        false
    )]
    [InlineData(
        Tests,
        4,
        "CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END",
        "CASE WHEN (u.deleted_at IS NULL) THEN 'open'::text ELSE 'closed'::text END",
        false
    )]
    [InlineData(
        Tests,
        5,
        "CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END",
        "CASE u.status WHEN 'active'::user_status THEN 'A'::text ELSE NULL::text END",
        null
    )]
    [InlineData(Tests, 6, "GREATEST(u.created_at, u.deleted_at)", "GREATEST(u.created_at, u.deleted_at)", null)]
    [InlineData(Tests, 7, "LEAST(1, u.id)", "LEAST(1, u.id)", false)]
    [InlineData(Tests, 8, "(u.email)::text", "(u.email)::text", null)]
    [InlineData(Tests, 9, "upper((u.name)::text)", "upper((u.name)::text)", null)]
    [InlineData(Tests, 10, "('x'::character varying(254))::email_t", "('x'::character varying(254))::email_t", null)]
    [InlineData(OneTable, 0, "((deleted_at IS NULL))", "((deleted_at IS NULL))", false)]
    [InlineData(OneTable, 1, "(GREATEST(created_at, deleted_at))", "(GREATEST(created_at, deleted_at))", null)]
    [InlineData(
        OneTable,
        2,
        "(COALESCE((email)::character varying, 'x'::character varying))",
        "(COALESCE((email)::character varying, 'x'::character varying))",
        false
    )]
    public async Task Explain_Form_PrintsWhatTheRecogniserReads(
        string sql,
        int ordinal,
        string printedOn14,
        string printedOn18,
        bool? expected
    )
    {
        var printed = postgres.Major switch
        {
            14 => printedOn14,
            18 => printedOn18,
            _ => null,
        };
        if (printed is null)
        {
            Assert.Skip("The printed forms are pinned for PostgreSQL 14 and 18 alone.");
        }

        var root = await ExplainAsync(sql);

        var entry = root.Output.ShouldNotBeNull()[ordinal];
        entry.ShouldBe(printed);
        PlanExpression.NullableOf(entry, catalogFunctions: true, static _ => false).ShouldBe(expected);
    }

    // The root of a set operation prints its first branch, whatever the operation.
    [Theory]
    [InlineData("SELECT 'a'::text AS k FROM users UNION SELECT NULL FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users INTERSECT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users EXCEPT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k UNION ALL SELECT NULL ORDER BY 1")]
    public async Task Explain_SetOperation_PrintsTheFirstBranchAtTheRoot(string sql)
    {
        var root = await ExplainAsync(sql);

        root.Output.ShouldNotBeNull()[0].ShouldBe("('a'::text)");
        Followed(root).ShouldContain(static node => node.NodeType == "Append" || node.NodeType == "SetOp");
    }

    // A branch of a set operation whose output needs a cast is a Subquery Scan aliased *SELECT* <k>, which prints a
    // column for each of the branch's, in the branch's order and under the branch's own names.  The first branch is
    // text and the second varchar, so the second is coerced; its names cross the first's, and its scan prints them in
    // its own order: the names say nothing of the place, and the place is the branch's.
    [Fact]
    public async Task Explain_SetOperationLeafScan_PrintsTheBranchInItsOrder()
    {
        var root = await ExplainAsync(
            "SELECT o.note AS label, o.note AS note FROM orders o "
                + "UNION ALL SELECT u.name AS note, u.email AS label FROM users u"
        );

        root.NodeType.ShouldBe("Append");
        root.Plans.Select(static member => member.ParentRelationship).ShouldBe(["Member", "Member"]);
        var scan = root.Plans[1];
        scan.NodeType.ShouldBe("Subquery Scan");
        scan.Alias.ShouldBe("*SELECT* 2");
        scan.Output.ShouldBe(["\"*SELECT* 2\".note", "\"*SELECT* 2\".label"]);
        var child = scan.Plans.Single(static child => child.ParentRelationship == "Subquery");
        child.Output.ShouldBe(["u.name", "u.email"]);
    }

    // The implicit cast of a branch to the set operation's type is not in the scan: it prints the column as the
    // branch's, int4, under a result of int8.  Only a node above the set operation prints the cast.
    [Fact]
    public async Task Explain_SetOperationLeafScan_HidesItsImplicitCast()
    {
        var root = await ExplainAsync("SELECT id FROM users UNION ALL SELECT id FROM orders");

        root.NodeType.ShouldBe("Append");
        var scan = root.Plans[0];
        scan.NodeType.ShouldBe("Subquery Scan");
        scan.Alias.ShouldBe("*SELECT* 1");
        scan.Output.ShouldBe(["\"*SELECT* 1\".id"]);
        scan.Plans.Single(static child => child.ParentRelationship == "Subquery").Output.ShouldBe(["users.id"]);
        root.Plans[1].Output.ShouldBe(["orders.id"]);
    }

    // INTERSECT and EXCEPT are a SetOp.  On 14 its one input is an Append of the branches, each ending in a flag of
    // its own, 0 for the left input and 1 for the right; on 18 it has two inputs, Outer and Inner, and no flag.
    [Theory]
    [InlineData("INTERSECT", "Intersect")]
    [InlineData("INTERSECT ALL", "Intersect All")]
    [InlineData("EXCEPT", "Except")]
    [InlineData("EXCEPT ALL", "Except All")]
    public async Task Explain_SetOp_HasFlaggedMembersOn14AndTwoInputsOn18(string keyword, string command)
    {
        if (postgres.Major is not (14 or 18))
        {
            Assert.Skip("The set operations are pinned for PostgreSQL 14 and 18 alone.");
        }

        var text = await ExplainTextAsync($"SELECT id FROM users {keyword} SELECT id FROM orders");

        var root = PlanNode.Read(text).ShouldNotBeNull();
        root.NodeType.ShouldBe("SetOp");
        using (var document = JsonDocument.Parse(text))
        {
            document.RootElement[0].GetProperty("Plan").GetProperty("Command").GetString().ShouldBe(command);
        }

        if (postgres.Major == 14)
        {
            var append = root.Plans.Single();
            append.ParentRelationship.ShouldBe("Outer");
            append.NodeType.ShouldBe("Append");
            append.Plans.ShouldAllBe(static member => member.ParentRelationship == "Member");
            append
                .Plans.Select(static member => member.Output.ShouldNotBeNull()[^1])
                .ShouldBe(["0", "1"], ignoreOrder: true);
        }
        else
        {
            root.Plans.Select(static child => child.ParentRelationship).ShouldBe(["Outer", "Inner"]);
            root.Plans.ShouldAllBe(static child => child.Output!.Count == 1);
        }
    }

    // EXCEPT keeps its left input first even where it is the smaller: the member of flag 0 on 14, the Outer child on
    // 18, whichever way the planner would order an INTERSECT.
    [Fact]
    public async Task Explain_Except_KeepsItsLeftInputFirst()
    {
        if (postgres.Major is not (14 or 18))
        {
            Assert.Skip("The set operations are pinned for PostgreSQL 14 and 18 alone.");
        }

        var root = await ExplainAsync(
            "SELECT id FROM users WHERE id < 3 EXCEPT SELECT g FROM generate_series(1, 100000) g"
        );

        root.NodeType.ShouldBe("SetOp");
        var (left, right) =
            postgres.Major == 14
                ? (
                    root.Plans.Single().Plans.Single(static member => member.Output![^1] == "0"),
                    root.Plans.Single().Plans.Single(static member => member.Output![^1] == "1")
                )
                : (
                    root.Plans.Single(static child => child.ParentRelationship == "Outer"),
                    root.Plans.Single(static child => child.ParentRelationship == "Inner")
                );
        All(left).Select(static node => node.RelationName).OfType<string>().ShouldBe(["users"]);
        All(right).Select(static node => node.RelationName).OfType<string>().ShouldBeEmpty();
    }

    // A branch's value counts for the result's type where the two are in one family of built-in types, every ordered
    // pair of which has a cast that the server ships: no user can add another, and none returns NULL for a value that
    // is not.
    [Fact]
    public async Task Catalog_CastFamilies_AreBuiltInBothWays()
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(
            """
            WITH family (id, type) AS (
                VALUES
                    (1, 'int2'::regtype), (1, 'int4'::regtype), (1, 'int8'::regtype), (1, 'numeric'::regtype),
                    (1, 'float4'::regtype), (1, 'float8'::regtype),
                    (2, 'text'::regtype), (2, 'varchar'::regtype), (2, 'bpchar'::regtype),
                    (3, 'date'::regtype), (3, 'timestamp'::regtype), (3, 'timestamptz'::regtype)
            ),
            pair AS (
                SELECT a.type AS source, b.type AS target
                FROM family a JOIN family b ON b.id = a.id AND b.type <> a.type
            )
            SELECT
                count(*) AS pairs,
                count(*) FILTER (
                    WHERE NOT EXISTS (
                        SELECT 1 FROM pg_cast c
                        WHERE c.castsource = p.source AND c.casttarget = p.target AND c.oid < 16384
                    )
                ) AS missing
            FROM pair p
            """
        );
        var token = TestContext.Current.CancellationToken;
        await using var reader = await command.ExecuteReaderAsync(token);
        (await reader.ReadAsync(token)).ShouldBeTrue();

        // Six types and three, three, make 30, 6 and 6 ordered pairs.
        reader.GetInt64(0).ShouldBe(42);
        reader.GetInt64(1).ShouldBe(0);
    }

    // A function the path finds before the catalog's prints as the catalog's would: the reason a bare name is the
    // catalog's only when the path names pg_catalog first.
    [Fact]
    public async Task Explain_CountShadowedOnTheSearchPath_PrintsUnqualified()
    {
        await RunAsync(ScratchSchema.ShadowCount);
        var connection = $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public";

        var root = await ExplainAsync("SELECT count(*) FROM users", connection);

        root.Output.ShouldNotBeNull().ShouldBe(["count(*)"]);
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand("SELECT count(*) IS NULL FROM users");
        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldBe(true);
    }

    // A Subquery Scan prints the subquery's columns in the order the query above needs them, and its child in the
    // subquery's own: the reason no column through a scan is mapped by its place.
    [Fact]
    public async Task Explain_SubqueryScan_PrintsItsColumnsInTheOrderTheyAreUsed()
    {
        var root = await ExplainAsync(
            "SELECT s.total, s.id FROM (SELECT o.id, o.total, row_number() OVER () AS n FROM orders o) s "
                + "WHERE s.n > 1"
        );

        var scan = Followed(root).Single(static node => node.NodeType == "Subquery Scan");
        scan.Output.ShouldBe(["s.total", "s.id"]);
        var child = scan.Plans.Single(static child => child.ParentRelationship == "Subquery");
        child.Output.ShouldNotBeNull().Take(2).ShouldBe(["o.id", "o.total"]);
    }

    // Each scan of a CTE names it, and the CTE's plan is one InitPlan named for it, wherever it hangs.
    [Fact]
    public async Task Explain_CteScan_NamesTheInitPlanOfItsCte()
    {
        var root = await ExplainAsync(
            "WITH recent AS (SELECT o.id, o.user_id FROM orders o) "
                + "SELECT r.id, n.id FROM recent r LEFT JOIN recent n ON n.user_id = r.user_id"
        );

        Followed(root)
            .Where(static node => node.NodeType == "CTE Scan")
            .Select(static node => (node.Alias, node.CteName))
            .ShouldBe([("r", "recent"), ("n", "recent")], ignoreOrder: true);
        var plans = All(root).Where(static node => node.ParentRelationship == "InitPlan").ToArray();
        plans.Single().SubplanName.ShouldBe("CTE recent");
        plans.Single().Output.ShouldBe(["o.id", "o.user_id"]);
    }

    // The NULL of a parameter prunes every partition when the executor starts, and EXPLAIN shows the Append with no
    // member.  Without partition pruning every member shows, each under the table's alias and a number, with the same
    // Output but for the alias, and the nodes above print the table's alias.
    [Fact]
    public async Task Explain_PartitionedTableWithoutPruning_PrintsEveryMember()
    {
        await RunAsync(ScratchSchema.Partitioned);
        const string Sql = "SELECT p.id, p.kind FROM scratch_parts p WHERE p.kind = $1 ORDER BY p.id";

        var pruned = await ExplainExecuteAsync(Sql, "integer", pruning: true);
        var unpruned = await ExplainExecuteAsync(Sql, "integer", pruning: false);

        Followed(pruned).Single(static node => node.NodeType == "Append").Plans.ShouldBeEmpty();
        unpruned.Output.ShouldBe(["p.id", "p.kind"]);
        var append = Followed(unpruned).Single(static node => node.NodeType == "Append");
        append.Output.ShouldBeNull();
        append
            .Plans.Select(static member => (member.ParentRelationship, member.Alias, member.RelationName))
            .ShouldBe([("Member", "p_1", "scratch_parts_1"), ("Member", "p_2", "scratch_parts_2")]);
        append.Plans[0].Output.ShouldBe(["p_1.id", "p_1.kind"]);
        append.Plans[1].Output.ShouldBe(["p_2.id", "p_2.kind"]);
    }

    // A value computed under a join prints in one pair of parentheses at every node above the node that computes
    // it, and as itself there; a value the join computes prints as itself at the join.
    [Fact]
    public async Task Explain_ValueComputedBelow_IsParenthesisedAboveIt()
    {
        var root = await ExplainAsync(
            "SELECT u.id, s.k, coalesce(s.note, 'z') AS z FROM users u LEFT JOIN "
                + "(SELECT o.user_id, 'x'::text AS k, o.note FROM orders o) s ON s.user_id = u.id "
                + "ORDER BY u.name LIMIT 5"
        );

        var path = Followed(root);
        var join = path.Single(static node => node.JoinType == "Right");
        root.Output.ShouldNotBeNull().Take(3).ShouldBe(["u.id", "('x'::text)", "(COALESCE(o.note, 'z'::text))"]);
        join.Output.ShouldNotBeNull().Take(3).ShouldBe(["u.id", "('x'::text)", "COALESCE(o.note, 'z'::text)"]);
        var scan = path.Single(static node => node.Alias == "o");
        scan.Output.ShouldNotBeNull().ShouldContain("'x'::text");
    }

    // The node and every node under it through the sides of a join or the one input of another node.
    private static PlanNode[] Followed(PlanNode node) =>
        [
            node,
            .. node.Plans.Where(static child => child.ParentRelationship is "Outer" or "Inner").SelectMany(Followed),
        ];

    // The node and every node under it, whatever the relationship.
    private static PlanNode[] All(PlanNode node) => [node, .. node.Plans.SelectMany(All)];
}

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// What PostgreSQL 14 and 18 print in a plan's Output for each form PlanExpression reads, and for the texts near
// them: a form is trusted only where a server is seen to print it so.  A row that fails on a server is a form the
// recogniser must not read as written.
[Collection(UsesPostgres.Name)]
public sealed class PlanOutputTests(PostgresContainer postgres)
{
    /// <summary>
    /// Aggregates <c>shadow.count(*)</c> and <c>shadow.count(integer)</c> that always return <c>NULL</c>, made by SQL
    /// that can be run again.  On a path that names <c>shadow</c> before <c>pg_catalog</c> the plan prints the first
    /// as <c>count(*)</c>; on any path that names <c>shadow</c>, the second beats <c>pg_catalog.count("any")</c> for an
    /// integer by its exact type and prints as <c>count(id)</c>.
    /// </summary>
    internal const string ShadowCount = """
        CREATE SCHEMA IF NOT EXISTS shadow;
        CREATE OR REPLACE FUNCTION shadow.nulls(int8) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(*) (sfunc = shadow.nulls, stype = int8);
        CREATE OR REPLACE FUNCTION shadow.nulls(int8, integer) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(integer) (sfunc = shadow.nulls, stype = int8);
        """;

    // A partitioned table of these tests alone, made by SQL that can be run again.
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS plan_parts (id integer NOT NULL, kind integer NOT NULL) PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS plan_parts_1 PARTITION OF plan_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS plan_parts_2 PARTITION OF plan_parts FOR VALUES IN (2);
        """;

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
    private async Task<PlanNode> ExplainAsync(string sql, string? connection = null)
    {
        await using var source = NpgsqlDataSource.Create(connection ?? postgres.ConnectionString);
        await using var command = source.CreateCommand("EXPLAIN (VERBOSE, FORMAT JSON) " + sql);
        var text = (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        return PlanNode.Read(text).ShouldNotBeNull();
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

    // The root of a set operation prints its first branch, so a constant there says nothing of the other branches.
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

    // A function the path finds before the catalog's prints as the catalog's would: the reason a bare name is the
    // catalog's only when the path names pg_catalog first.
    [Fact]
    public async Task Explain_CountShadowedOnTheSearchPath_PrintsUnqualified()
    {
        await RunAsync(ShadowCount);
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
        await RunAsync(Partitioned);
        const string Sql = "SELECT p.id, p.kind FROM plan_parts p WHERE p.kind = $1 ORDER BY p.id";

        var pruned = await ExplainExecuteAsync(Sql, "integer", pruning: true);
        var unpruned = await ExplainExecuteAsync(Sql, "integer", pruning: false);

        Followed(pruned).Single(static node => node.NodeType == "Append").Plans.ShouldBeEmpty();
        unpruned.Output.ShouldBe(["p.id", "p.kind"]);
        var append = Followed(unpruned).Single(static node => node.NodeType == "Append");
        append.Output.ShouldBeNull();
        append
            .Plans.Select(static member => (member.ParentRelationship, member.Alias, member.RelationName))
            .ShouldBe([("Member", "p_1", "plan_parts_1"), ("Member", "p_2", "plan_parts_2")]);
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

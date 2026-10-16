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
    /// An aggregate <c>shadow.count(*)</c> that always returns <c>NULL</c>, made by SQL that can be run again.  On a
    /// path that names <c>shadow</c> before <c>pg_catalog</c> the plan prints it as <c>count(*)</c>.
    /// </summary>
    internal const string ShadowCount = """
        CREATE SCHEMA IF NOT EXISTS shadow;
        CREATE OR REPLACE FUNCTION shadow.nulls(int8) RETURNS int8 LANGUAGE sql AS 'SELECT NULL::int8';
        CREATE OR REPLACE AGGREGATE shadow.count(*) (sfunc = shadow.nulls, stype = int8);
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
    [InlineData(Windows, 2, "(count(DISTINCT name))", "(count(DISTINCT name))", false)]
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

    // The node and every node under it through the sides of a join or the one input of another node.
    private static PlanNode[] Followed(PlanNode node) =>
        [
            node,
            .. node.Plans.Where(static child => child.ParentRelationship is "Outer" or "Inner").SelectMany(Followed),
        ];
}

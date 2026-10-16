using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A column of no origin answered from the plan, against a real server: the forms the plan shows cannot be NULL, and
// the shapes where the same text can be, which keep no-origin.
[Collection(UsesPostgres.Name)]
public sealed class ExpressionTests(PostgresContainer postgres)
{
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS walk_parts (id integer NOT NULL, kind integer NOT NULL) PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS walk_parts_1 PARTITION OF walk_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS walk_parts_2 PARTITION OF walk_parts FOR VALUES IN (2);
        """;

    private static readonly (bool?, string?) NotNull = (false, "expression");

    private static readonly (bool?, string?) Unknown = (null, "no-origin");

    private async Task<(bool? Nullable, string? Source)[]> AnswersAsync(string sql, string? connection = null)
    {
        var described = await Sessions.DescribeAsync(connection ?? postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        return
        [
            .. described
                .Value.ShouldNotBeNull()
                .Columns.ShouldNotBeNull()
                .Select(static column => (column.Nullable, column.NullableSource)),
        ];
    }

    private async Task RunAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Describe_ExpressionsOfOneTable_AreAnsweredFromThePlan() =>
        (
            await AnswersAsync(
                "SELECT a.id, coalesce(a.email, 'none') AS contact, "
                    + "CASE WHEN a.deleted_at IS NULL THEN 'open' ELSE 'closed' END AS state, "
                    + "CASE a.status WHEN 'active' THEN 'A' END AS label, "
                    + "greatest(a.created_at, a.deleted_at) AS changed, a.deleted_at IS NULL AS live, "
                    + "'v1' AS version, NULL::text AS nothing, a.email::text AS raw FROM users a ORDER BY a.id"
            )
        ).ShouldBe([
            (false, "catalog"),
            NotNull,
            NotNull,
            Unknown,
            NotNull,
            NotNull,
            NotNull,
            (true, "expression"),
            Unknown,
        ]);

    [Fact]
    public async Task Describe_CountsAndRankings_AreAnsweredFromThePlan() =>
        (
            await AnswersAsync(
                "SELECT id, count(*) AS n, count(*) FILTER (WHERE status = 'active') AS active, "
                    + "count(DISTINCT name) AS names, count(*) OVER () AS total, "
                    + "row_number() OVER (ORDER BY id) AS position, rank() OVER (ORDER BY id) AS place "
                    + "FROM users GROUP BY id"
            )
        ).ShouldBe([(false, "catalog"), NotNull, NotNull, Unknown, NotNull, NotNull, NotNull]);

    // An aggregate of another schema whose argument's type is exact beats pg_catalog.count("any") whatever the path's
    // order, and the plan prints it bare: only count(*) is the catalog's by its text.
    [Fact]
    public async Task Describe_CountOfAnArgumentThatAnotherSchemaTakes_StaysUnknown()
    {
        await RunAsync(PlanOutputTests.ShadowCount);

        (
            await AnswersAsync(
                "SELECT count(u.id) AS n, count(*) AS m FROM users u",
                $"{postgres.ConnectionString};Search Path=pg_catalog,shadow,public"
            )
        ).ShouldBe([Unknown, NotNull]);
    }

    // The root of a set operation prints its first branch: a constant there says nothing of the NULL of the other.
    [Theory]
    [InlineData("SELECT 'a'::text AS k FROM users UNION SELECT NULL FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users INTERSECT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users EXCEPT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k UNION ALL SELECT NULL ORDER BY 1")]
    [InlineData(
        "WITH RECURSIVE r(k, n) AS (SELECT 'a'::text, 1 UNION ALL SELECT NULL, n + 1 FROM r WHERE n < 3) "
            + "SELECT k, n FROM r"
    )]
    public async Task Describe_ConstantFirstBranchOfASetOperation_StaysUnknown(string sql) =>
        (await AnswersAsync(sql))[0].ShouldBe(Unknown);

    // A value computed on the side a LEFT JOIN extends prints at the root as the same text, and is NULL for a row the
    // join did not match.
    [Theory]
    [InlineData(
        "SELECT u.id, s.k, s.c FROM users u LEFT JOIN "
            + "(SELECT o.user_id, 'x'::text AS k, coalesce(o.note, 'n') AS c FROM orders o) s ON s.user_id = u.id"
    )]
    [InlineData(
        "SELECT u.id, c.n, c.n FROM users u "
            + "LEFT JOIN LATERAL (SELECT count(*) AS n FROM orders o WHERE o.user_id = u.id) c ON true"
    )]
    public async Task Describe_ExpressionFromTheSideALeftJoinExtends_StaysUnknown(string sql) =>
        (await AnswersAsync(sql)).Skip(1).ShouldBe([Unknown, Unknown]);

    // A view brings a LEFT JOIN ... IS NULL into a query without a word of the gate, and the anti join it plans as
    // returns the pulled-up constant NULL.
    [Fact]
    public async Task Describe_ExpressionUnderAnAntiJoinOfAView_StaysUnknown()
    {
        await RunAsync(
            "CREATE OR REPLACE VIEW expression_lonely AS SELECT u.id, s.k FROM users u "
                + "LEFT JOIN (SELECT o.user_id, 'x'::text AS k FROM orders o) s ON s.user_id = u.id "
                + "WHERE s.user_id IS NULL"
        );

        (
            await AnswersAsync(
                "SELECT CASE WHEN l.id > 0 THEN l.k ELSE 'y' END AS k2, coalesce(l.k, NULL) AS k3 "
                    + "FROM expression_lonely l"
            )
        ).ShouldBe([Unknown, Unknown]);
    }

    // A NOT EXISTS plans as an anti join too, which the text cannot tell from the view's.
    [Fact]
    public async Task Describe_ExpressionBesideNotExists_StaysUnknown() =>
        (
            await AnswersAsync(
                "SELECT u.id, 'k' AS k FROM users u WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)"
            )
        ).ShouldBe([(false, "catalog"), Unknown]);

    // A partitioned table plans as an Append, which the text cannot tell from a UNION's.
    [Fact]
    public async Task Describe_PartitionedTableAggregate_StaysUnknown()
    {
        await RunAsync(Partitioned);

        (await AnswersAsync("SELECT count(*) AS n FROM walk_parts")).ShouldBe([Unknown]);
    }

    // A count the path finds before pg_catalog's may be another schema's, which can return NULL.
    [Fact]
    public async Task Describe_CountShadowedOnTheSearchPath_StaysUnknown()
    {
        await RunAsync(PlanOutputTests.ShadowCount);

        (
            await AnswersAsync(
                "SELECT count(*) AS n FROM users",
                $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public"
            )
        ).ShouldBe([Unknown]);
        (
            await AnswersAsync(
                "SELECT count(*) AS n FROM users",
                $"{postgres.ConnectionString};Search Path=pg_catalog,shadow,public"
            )
        ).ShouldBe([NotNull]);
    }
}

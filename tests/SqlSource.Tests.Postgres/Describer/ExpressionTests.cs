using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;

namespace SqlSource.Tests.Postgres.Describer;

// A column of no origin answered from the plan, against a real server: the forms the plan shows cannot be NULL, and
// the shapes where the same text can be, which keep no-origin.
[Collection(UsesPostgres.Name)]
public sealed class ExpressionTests(PostgresContainer postgres)
{
    // A table whose NOT NULL column is a domain, a type that no signature of the tables takes.
    private const string AmountsTable =
        "CREATE TABLE IF NOT EXISTS expression_amounts (id integer NOT NULL PRIMARY KEY, amount money_t NOT NULL)";

    private async Task<(bool? Nullable, string? Source)[]> AnswersAsync(string sql, string? connection = null)
    {
        var description = await Sessions.ShouldDescribeAsync(connection ?? postgres.ConnectionString, sql);

        return Of(description.Columns.ShouldNotBeNull());
    }

    [Fact]
    public async Task DescribeAsync_ExpressionsOfOneTable_AreAnsweredFromThePlan() =>
        (
            await AnswersAsync(
                "SELECT a.id, coalesce(a.email, 'none') AS contact, "
                    + "CASE WHEN a.deleted_at IS NULL THEN 'open' ELSE 'closed' END AS state, "
                    + "CASE a.status WHEN 'active' THEN 'A' END AS label, "
                    + "greatest(a.created_at, a.deleted_at) AS changed, a.deleted_at IS NULL AS live, "
                    + "'v1' AS version, NULL::text AS nothing, a.email::text AS raw FROM users a ORDER BY a.id"
            )
        ).ShouldBe([
            NotNullCatalog,
            NotNullExpression,
            NotNullExpression,
            UnknownNoOrigin,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            NullableExpression,
            UnknownNoOrigin,
        ]);

    [Fact]
    public async Task DescribeAsync_CountsAndRankings_AreAnsweredFromThePlan() =>
        (
            await AnswersAsync(
                "SELECT id, count(*) AS n, count(*) FILTER (WHERE status = 'active') AS active, "
                    + "count(DISTINCT name) AS names, count(*) OVER () AS total, "
                    + "row_number() OVER (ORDER BY id) AS position, rank() OVER (ORDER BY id) AS place "
                    + "FROM users GROUP BY id"
            )
        ).ShouldBe([
            NotNullCatalog,
            NotNullExpression,
            NotNullExpression,
            UnknownNoOrigin,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
        ]);

    // An aggregate of another schema whose argument's type is exact beats pg_catalog.count("any") whatever the path's
    // order, and the plan prints it bare: only count(*) is the catalog's by its text.
    [Fact]
    public async Task DescribeAsync_CountOfAnArgumentThatAnotherSchemaTakes_StaysUnknown()
    {
        await postgres.ExecuteAsync(ScratchSchema.ShadowCount);

        (
            await AnswersAsync(
                "SELECT count(u.id) AS n, count(*) AS m FROM users u",
                $"{postgres.ConnectionString};Search Path=pg_catalog,shadow,public"
            )
        ).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // The root of a set operation prints its first branch: a constant there says nothing of the NULL of the other.
    [Theory]
    [InlineData("SELECT 'a'::text AS k FROM users UNION SELECT NULL FROM orders")]
    [InlineData("SELECT 'a'::text AS k UNION ALL SELECT NULL ORDER BY 1")]
    [InlineData(
        "WITH RECURSIVE r(k, n) AS (SELECT 'a'::text, 1 UNION ALL SELECT NULL, n + 1 FROM r WHERE n < 3) "
            + "SELECT k, n FROM r"
    )]
    public async Task DescribeAsync_ConstantFirstBranchOfASetOperation_StaysUnknown(string sql) =>
        (await AnswersAsync(sql))[0].ShouldBe(UnknownNoOrigin);

    // An INTERSECT returns rows that both inputs hold, and an EXCEPT rows of its left input: the constant of the
    // first branch proves the column whatever the other holds.
    [Theory]
    [InlineData("SELECT 'a'::text AS k FROM users INTERSECT SELECT note FROM orders")]
    [InlineData("SELECT 'a'::text AS k FROM users EXCEPT SELECT note FROM orders")]
    public async Task DescribeAsync_ConstantFirstBranchOfAnIntersectOrAnExcept_IsAnsweredFromIt(string sql) =>
        (await AnswersAsync(sql))[0].ShouldBe(NotNullSetOperation);

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
    public async Task DescribeAsync_ExpressionFromTheSideALeftJoinExtends_StaysUnknown(string sql) =>
        (await AnswersAsync(sql)).Skip(1).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A view brings a LEFT JOIN ... IS NULL into a query without a word of the gate, and the anti join it plans as
    // returns the pulled-up constant NULL.
    [Fact]
    public async Task DescribeAsync_ExpressionUnderAnAntiJoinOfAView_StaysUnknown()
    {
        await postgres.ExecuteAsync(
            "CREATE OR REPLACE VIEW expression_lonely AS SELECT u.id, s.k FROM users u "
                + "LEFT JOIN (SELECT o.user_id, 'x'::text AS k FROM orders o) s ON s.user_id = u.id "
                + "WHERE s.user_id IS NULL"
        );

        (
            await AnswersAsync(
                "SELECT CASE WHEN l.id > 0 THEN l.k ELSE 'y' END AS k2, coalesce(l.k, NULL) AS k3 "
                    + "FROM expression_lonely l"
            )
        ).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // A NOT EXISTS plans as an anti join too, which the text cannot tell from the view's.
    // The root computes the constant over the anti join, which no join above it extends.
    [Fact]
    public async Task DescribeAsync_ExpressionBesideNotExists_IsAnswered() =>
        (
            await AnswersAsync(
                "SELECT u.id, 'k' AS k FROM users u WHERE NOT EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)"
            )
        ).ShouldBe([NotNullCatalog, NotNullExpression]);

    // A partitioned table plans as an Append, which "partitions" tells from a UNION's.
    [Fact]
    public async Task DescribeAsync_PartitionedTableAggregate_IsAnswered()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        (await AnswersAsync("SELECT count(*) AS n FROM scratch_parts")).ShouldBe([NotNullExpression]);
    }

    // A count the path finds before pg_catalog's may be another schema's, which can return NULL.
    [Fact]
    public async Task DescribeAsync_CountShadowedOnTheSearchPath_StaysUnknown()
    {
        await postgres.ExecuteAsync(ScratchSchema.ShadowCount);

        (
            await AnswersAsync(
                "SELECT count(*) AS n FROM users",
                $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public"
            )
        ).ShouldBe([UnknownNoOrigin]);
        (
            await AnswersAsync(
                "SELECT count(*) AS n FROM users",
                $"{postgres.ConnectionString};Search Path=pg_catalog,shadow,public"
            )
        ).ShouldBe([NotNullExpression]);
    }

    // An operator, a function or a cast of pg_catalog that is total, over values the plan proves, cannot be NULL.  A
    // value the plan cannot prove, a column that may be NULL or a range, keeps no-origin.
    [Fact]
    public async Task DescribeAsync_TotalOperatorsAndFunctions_AreAnsweredFromThePlan() =>
        (
            await AnswersAsync(
                "SELECT u.balance * 2 AS twice, u.id::bigint * 2 AS wide, lower(u.name) || '!' AS shout, "
                    + "length(u.name) AS size, date_trunc('day', u.created_at) AS day, u.created_at::date AS born, "
                    + "u.name = 'x' AS named, u.email || '!' AS mail, upper(b.during) AS ends "
                    + "FROM users u JOIN bookings b ON b.user_id = u.id"
            )
        ).ShouldBe([
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            UnknownNoOrigin,
            UnknownNoOrigin,
        ]);

    // A cast outside a family, an operator or a function that the closed table leaves out, and a value of a type
    // outside the tables (an enum, an array, a domain) keep no-origin whatever the arguments.
    [Fact]
    public async Task DescribeAsync_FormsOutsideTheTables_KeepNoOrigin()
    {
        await postgres.ExecuteAsync(AmountsTable);

        (
            await AnswersAsync(
                "SELECT u.id::text AS label, u.name::integer AS parsed, u.id % 2 AS odd, abs(u.balance) AS size, "
                    + "u.status = 'active' AS active, u.tags || 'x'::text AS more, d.amount * 2 AS twice "
                    + "FROM users u JOIN expression_amounts d ON d.id = u.id"
            )
        ).ShouldBe(Enumerable.Repeat(UnknownNoOrigin, 7));
    }

    // An aggregate other than count, at the node that computes it under a GROUP BY, sees every row of a group, and
    // a group has a row.  A FILTER may leave none, a polymorphic aggregate is left, and max(o.note) sees a column
    // that may be NULL.
    [Fact]
    public async Task DescribeAsync_AggregatesOverAGroup_AreAnswered() =>
        (
            await AnswersAsync(
                "SELECT u.id, max(o.placed_at), sum(o.total), string_agg(u.name, ','), max(o.note), "
                    + "max(o.placed_at) FILTER (WHERE o.total > 0), array_agg(o.id) "
                    + "FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id"
            )
        ).ShouldBe([
            NotNullCatalog,
            NotNullExpression,
            NotNullExpression,
            NotNullExpression,
            UnknownNoOrigin,
            UnknownNoOrigin,
            UnknownNoOrigin,
        ]);

    // The aggregate of a type outside the families, over an enum or a domain, and a DISTINCT or an ORDER BY inside
    // an aggregate of the table, which change nothing.
    [Fact]
    public async Task DescribeAsync_AggregatesOfTypesOutsideTheTables_KeepNoOrigin()
    {
        await postgres.ExecuteAsync(AmountsTable);

        (
            await AnswersAsync(
                "SELECT d.id, sum(d.amount), max(d.amount), max(u.status) "
                    + "FROM expression_amounts d JOIN users u ON u.id = d.id GROUP BY d.id"
            )
        ).ShouldBe([NotNullCatalog, UnknownNoOrigin, UnknownNoOrigin, UnknownNoOrigin]);
        (
            await AnswersAsync(
                "SELECT o.user_id, max(DISTINCT o.total), string_agg(u.name, ',' ORDER BY u.name) "
                    + "FROM users u JOIN orders o ON o.user_id = u.id GROUP BY o.user_id"
            )
        ).ShouldBe([NotNullCatalog, NotNullExpression, NotNullExpression]);
    }

    // Each aggregate may meet no row, and keeps no-origin even over a NOT NULL column.  With no GROUP BY or with
    // GROUP BY () the node is plain; a LEFT JOIN may extend the group's rows; a window aggregate's frame may be empty;
    // the fifth reads the value of a plain aggregate through a subquery; and in the sixth the outer aggregate is
    // plain, so a grouped subquery under it may hold no group.  An aggregate inside a span that a child of a grouped
    // node printed is owned by the unit tests of NullabilityWalkTests.
    [Theory]
    [InlineData("SELECT max(created_at), sum(balance) FROM users", 0)]
    [InlineData("SELECT max(created_at), sum(balance) FROM users GROUP BY ()", 0)]
    [InlineData("SELECT u.id, max(o.placed_at) FROM users u LEFT JOIN orders o ON o.user_id = u.id GROUP BY u.id", 1)]
    [InlineData("SELECT sum(balance) OVER (ORDER BY id ROWS BETWEEN 1 FOLLOWING AND 2 FOLLOWING) FROM users", 0)]
    [InlineData("SELECT s.m + 1 AS next FROM (SELECT max(o.total) AS m FROM orders o) s", 0)]
    [InlineData(
        "SELECT max(s.m) + 1 AS next FROM (SELECT o.user_id, max(o.total) AS m FROM orders o GROUP BY o.user_id) s",
        0
    )]
    public async Task DescribeAsync_AggregatesThatMayBeNull_KeepNoOrigin(string sql, int keys)
    {
        var answers = await AnswersAsync(sql);

        answers.Length.ShouldBeGreaterThan(keys);
        answers.Take(keys).ShouldBe(Enumerable.Repeat(NotNullCatalog, keys));
        answers.Skip(keys).ShouldBe(Enumerable.Repeat(UnknownNoOrigin, answers.Length - keys));
    }

    // A scalar subquery of a plain count returns one row that is a number.
    [Fact]
    public async Task DescribeAsync_ScalarSubqueryOfACount_IsAnswered() =>
        (
            await AnswersAsync(
                "SELECT u.id, (SELECT count(*) FROM orders o WHERE o.user_id = u.id) AS orders FROM users u"
            )
        ).ShouldBe([NotNullCatalog, NotNullExpression]);

    private const string InOverAPlainCount =
        "SELECT u.id IN (SELECT count(*) FROM orders o WHERE o.user_id = u.id) AS counted FROM users u";

    // Before 18 the plan prints an IN over a plain count as (SubPlan n), the text of a scalar subquery.  The column is
    // a boolean, outside the families a scalar subquery is read in, so it stays unknown.
    [Fact]
    public async Task DescribeAsync_InOverAPlainCount_KeepsNoOrigin()
    {
        Assert.SkipWhen(postgres.Major >= 18, "PostgreSQL 18 prints an IN as an ANY, which it answers.");

        (await AnswersAsync(InOverAPlainCount)).ShouldBe([UnknownNoOrigin]);
    }

    // From 18 the plan prints the same IN as an ANY over the subplan's column, whose left side and count are both
    // not NULL, and it is answered.
    [Fact]
    public async Task DescribeAsync_InOverAPlainCount_IsAnsweredOnEighteen()
    {
        postgres.SkipBelow(18, "PostgreSQL before 18 prints an IN as (SubPlan n), which is unknown.");

        (await AnswersAsync(InOverAPlainCount)).ShouldBe([NotNullExpression]);
    }

    // max over no row is NULL, and so is a scalar subquery of no row.
    [Theory]
    [InlineData("SELECT (SELECT max(o.total) FROM orders o WHERE o.user_id = u.id) AS biggest FROM users u")]
    [InlineData("SELECT (SELECT o.total FROM orders o WHERE o.user_id = u.id LIMIT 1) AS first FROM users u")]
    [InlineData(
        "SELECT (SELECT max(o.total) FROM orders o WHERE o.user_id = u.id GROUP BY o.user_id) AS g FROM users u"
    )]
    public async Task DescribeAsync_ScalarSubqueryOfAMax_KeepsNoOrigin(string sql) =>
        (await AnswersAsync(sql)).ShouldBe([UnknownNoOrigin]);

    // 18 prints EXISTS and the comparison of an IN as their own forms, and 14 prints the same (SubPlan n) for them
    // and for a scalar subquery, so it is unknown there.
    [Fact]
    public async Task DescribeAsync_ExistsAndAny_AreAnsweredOnEighteen() =>
        (
            await AnswersAsync(
                "SELECT EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id) AS has_orders, "
                    + "u.id IN (SELECT o.user_id FROM orders o) AS ordered FROM users u"
            )
        ).ShouldBe(postgres.Major >= 18 ? [NotNullExpression, NotNullExpression] : [UnknownNoOrigin, UnknownNoOrigin]);

    // An IN with a side that may be NULL is NULL for a row the comparison is unknown on.
    [Theory]
    [InlineData("SELECT u.email IN (SELECT o.note FROM orders o) AS a FROM users u")]
    [InlineData("SELECT u.name IN (SELECT o.note FROM orders o) AS a FROM users u")]
    [InlineData("SELECT u.deleted_at IN (SELECT o.placed_at FROM orders o) AS a FROM users u")]
    public async Task DescribeAsync_AnyWithASideThatMayBeNull_KeepsNoOrigin(string sql) =>
        (await AnswersAsync(sql)).ShouldBe([UnknownNoOrigin]);

    // Neither side of this IN may be NULL, but its plan compares rows, and only a single comparison is read.
    [Fact]
    public async Task DescribeAsync_RowComparisonIn_KeepsNoOrigin() =>
        (
            await AnswersAsync(
                "SELECT (u.id, u.name) IN (SELECT o.user_id, o.id::text FROM orders o) AS a FROM users u"
            )
        ).ShouldBe([UnknownNoOrigin]);

    // xmax of a table is not NULL: the plan prints it as a column, and tables holds no row for it.
    [Fact]
    public async Task DescribeAsync_SystemColumn_IsAnswered() =>
        (
            await AnswersAsync(
                "INSERT INTO orders (id, user_id, total) VALUES (@id, @user_id, @total) "
                    + "ON CONFLICT (id) DO UPDATE SET total = EXCLUDED.total RETURNING id, (xmax = 0) AS inserted"
            )
        ).ShouldBe([NotNullCatalog, NotNullExpression]);

    // The system column of a relation a LEFT JOIN extends is NULL for a row it did not match.
    [Fact]
    public async Task DescribeAsync_SystemColumnOfAnExtendedRelation_KeepsNoOrigin() =>
        (
            await AnswersAsync(
                "SELECT (u.xmin = 0) AS own, (o.xmin = 0) AS extended "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id"
            )
        ).ShouldBe([NotNullExpression, UnknownNoOrigin]);

    [Fact]
    public async Task DescribeAsync_MergeAction_IsAnswered()
    {
        postgres.SkipBelow(17, "PostgreSQL before 17 has no RETURNING for MERGE.");

        (
            await AnswersAsync(
                "MERGE INTO orders AS o USING users AS u ON o.user_id = u.id "
                    + "WHEN MATCHED THEN UPDATE SET note = 'seen' "
                    + "WHEN NOT MATCHED THEN DO NOTHING RETURNING merge_action() AS action, o.id"
            )
        ).ShouldBe([NotNullExpression, NotNullCatalog]);
    }

    // A path that finds a schema before pg_catalog may choose its operator or aggregate for the same argument types,
    // and the plan prints it bare.  The same queries, with pg_catalog first, are answered.
    [Fact]
    public async Task DescribeAsync_OperatorOfASchemaBeforeTheCatalog_KeepsNoOrigin()
    {
        await postgres.ExecuteAsync(ScratchSchema.ShadowOperators);
        const string Product = "SELECT u.id, u.balance * 2 AS twice FROM users u";
        const string Latest =
            "SELECT u.id, max(o.placed_at) AS latest FROM users u JOIN orders o ON o.user_id = u.id GROUP BY u.id";

        var shadowed = $"{postgres.ConnectionString};Search Path=shadow,pg_catalog,public";
        await using (var source = NpgsqlDataSource.Create(shadowed))
        {
            // The premise: on this path the shadow operator and aggregate are the ones the server chooses.
            await using var command = source.CreateCommand(
                "SELECT 2::numeric * 2::numeric IS NULL, (SELECT max(x) FROM (VALUES (now())) v (x)) IS NULL"
            );
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
            (await reader.ReadAsync(TestContext.Current.CancellationToken)).ShouldBeTrue();
            (reader.GetBoolean(0), reader.GetBoolean(1)).ShouldBe((true, true));
        }

        (await AnswersAsync(Product, shadowed)).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
        (await AnswersAsync(Latest, shadowed)).ShouldBe([NotNullCatalog, UnknownNoOrigin]);

        var catalogFirst = $"{postgres.ConnectionString};Search Path=pg_catalog,shadow,public";
        (await AnswersAsync(Product, catalogFirst)).ShouldBe([NotNullCatalog, NotNullExpression]);
        (await AnswersAsync(Latest, catalogFirst)).ShouldBe([NotNullCatalog, NotNullExpression]);
    }
}

using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;

namespace SqlSource.Tests.Postgres.Describer;

// What the walk answers against a real server through a scan: a column through a scan of another plan, a partitioned
// table, a foreign table and a root that is not the scan, and an expression the walk follows to the node that computes
// it; and the shapes near them that keep what they had.
[Collection(UsesPostgres.Name)]
public sealed class ScanTests(PostgresContainer postgres)
{
    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        var (plan, answers) = await DescribedAsync(sql);

        plan.ShouldBe(SidecarValues.Plan.Walked);
        return answers;
    }

    private async Task<(string? Plan, (bool? Nullable, string? Source)[] Answers)> DescribedAsync(string sql)
    {
        var description = await Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql);

        return (description.Plan, Of(description.Columns.ShouldNotBeNull()));
    }

    // The row-number dedupe: the subquery is planned apart, under a Subquery Scan, and its LEFT JOIN extends orders.
    [Fact]
    public async Task DescribeAsync_ColumnsThroughASubqueryScan_AreAnsweredByTheirOrigins() =>
        (
            await WalkedAsync(
                "SELECT s.id, s.name, s.total FROM (SELECT u.id, u.name, o.total, "
                    + "row_number() OVER (PARTITION BY u.id ORDER BY o.placed_at DESC) AS rn "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id) s WHERE s.rn = 1 ORDER BY s.name"
            )
        ).ShouldBe([NotNullCatalog, NotNullCatalog, NullableOuterJoin]);

    // Matched by name, a column renamed to another's name would be read as the other.
    [Fact]
    public async Task DescribeAsync_SubqueryColumnsRenamedToEachOthersNames_AreNotReadCrossed() =>
        (
            await WalkedAsync(
                "SELECT s.total, s.note FROM (SELECT o.note AS total, o.total AS note, row_number() OVER () AS n "
                    + "FROM orders o LEFT JOIN users u ON u.id = o.user_id) s WHERE s.n > 1"
            )
        ).ShouldBe([NullableCatalog, NotNullCatalog]);

    // Both columns are users.id, of the side the join keeps and of the side it extends: which is which through the
    // scan cannot be told.
    [Fact]
    public async Task DescribeAsync_SubqueryColumnsOfASelfJoin_KeepTheHeuristic() =>
        (
            await WalkedAsync(
                "SELECT s.a, s.b FROM (SELECT u.id AS a, v.id AS b, row_number() OVER () AS n "
                    + "FROM users u LEFT JOIN users v ON v.id = u.id + 1) s WHERE s.n > 0"
            )
        ).ShouldBe([NullableHeuristic, NullableHeuristic]);

    // One plan of the CTE, and two scans of it, the second on the side the join extends.
    [Fact]
    public async Task DescribeAsync_ColumnsOfACteUsedTwice_AreAnsweredForEachScan() =>
        (
            await WalkedAsync(
                "WITH recent AS MATERIALIZED (SELECT o.id, o.user_id, o.note FROM orders o) "
                    + "SELECT r.id, r.note, n.id AS later FROM recent r "
                    + "LEFT JOIN recent n ON n.user_id = r.user_id AND n.id > r.id"
            )
        ).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);

    // A condition on a parameter prunes at executor start, so the plan keeps the Append of every partition.
    [Fact]
    public async Task DescribeAsync_PartitionedTableOnEachSideOfALeftJoin_IsAnsweredByItsSide()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        (
            await WalkedAsync(
                "SELECT p.id, p.note, u.name FROM scratch_parts p LEFT JOIN users u ON u.id = p.id WHERE p.kind = @kind"
            )
        ).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
        (
            await WalkedAsync(
                "SELECT u.id, p.id FROM users u LEFT JOIN scratch_parts p ON p.id = u.id AND p.kind = @kind"
            )
        ).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // A partitioned table is no set operation: an aggregate over it is answered.
    [Fact]
    public async Task DescribeAsync_AggregateOverAPartitionedTable_IsAnswered()
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        (await WalkedAsync("SELECT count(*) AS n, 'p' AS k FROM scratch_parts p WHERE p.kind = @kind")).ShouldBe([
            NotNullExpression,
            NotNullExpression,
        ]);
    }

    // A UNION ALL of two partitions of one table whose branches differ is a set operation, whose root prints the
    // first branch: NULL in the second branch, the column stays unknown, whatever the plan.
    [Theory]
    [InlineData(
        "SELECT y.v FROM (SELECT x.kind AS v FROM scratch_parts_1 x "
            + "UNION ALL SELECT NULL::integer FROM scratch_parts_2 x) y"
    )]
    [InlineData(
        "SELECT greatest(y.v, y.w) FROM (SELECT x.kind AS v, x.id AS w FROM scratch_parts_1 x "
            + "UNION ALL SELECT NULL::integer, NULL::integer FROM scratch_parts_2 x) y"
    )]
    public async Task DescribeAsync_UnionAllOfTwoPartitionsWithOtherBranches_StaysUnknown(string sql)
    {
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);

        (await DescribedAsync(sql)).Answers.ShouldBe([UnknownNoOrigin]);
    }

    // A foreign table's scan is an instance: unknown where nothing extends it, nullable where a join does.  The
    // foreign table, scan_foreign, is orders, read back through postgres_fdw from this very server.
    [Fact]
    public async Task DescribeAsync_ForeignTableOnEachSideOfALeftJoin_IsAnsweredByItsSide()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        await postgres.ExecuteAsync(
            "CREATE EXTENSION IF NOT EXISTS postgres_fdw; "
                + "DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM pg_foreign_server WHERE srvname = 'scan_loop') THEN "
                + "CREATE SERVER scan_loop FOREIGN DATA WRAPPER postgres_fdw "
                + $"OPTIONS (host 'localhost', dbname '{connection.Database}'); "
                + "CREATE USER MAPPING FOR CURRENT_USER SERVER scan_loop "
                + $"OPTIONS (user '{connection.Username}', password '{connection.Password}'); "
                + "END IF; END $$; "
                + "CREATE FOREIGN TABLE IF NOT EXISTS scan_foreign (id bigint NOT NULL, note text) "
                + "SERVER scan_loop OPTIONS (table_name 'orders')"
        );

        (await WalkedAsync("SELECT f.id, u.name FROM scan_foreign f LEFT JOIN users u ON u.id = f.id")).ShouldBe([
            UnknownForeignTable,
            NullableOuterJoin,
        ]);
        (await WalkedAsync("SELECT u.id, f.id FROM users u LEFT JOIN scan_foreign f ON f.id = u.id")).ShouldBe([
            NotNullCatalog,
            NullableOuterJoin,
        ]);
    }

    // A plan of one relation prints bare names at a Limit over a Sort over the scan.  The function's name gates it.
    [Fact]
    public async Task DescribeAsync_BareNamesAtARootOverOneScan_AreTheRelations() =>
        (
            await WalkedAsync(
                "SELECT id, name, deleted_at FROM users WHERE left(name, 1) = @initial ORDER BY name LIMIT 10"
            )
        ).ShouldBe([NotNullCatalog, NotNullCatalog, NullableCatalog]);

    // A value the root computes over a join that extends is answered: a COALESCE with a NOT NULL argument of the side
    // a RIGHT JOIN keeps, whose table a plain column names, and a constant.  A constant of a subquery pulled up from
    // the side a LEFT JOIN extends is computed at its scan, and stays unknown.
    [Fact]
    public async Task DescribeAsync_ExpressionTheRootComputesOverAnOuterJoin_IsAnswered() =>
        (
            await WalkedAsync(
                "SELECT o.id, coalesce(a.id, o.user_id) AS uid, 'k' AS k, s.c FROM users a "
                    + "RIGHT JOIN orders o ON o.user_id = a.id "
                    + "LEFT JOIN (SELECT o2.id, 'x'::text AS c FROM orders o2) s ON s.id = o.id"
            )
        ).ShouldBe([NotNullCatalog, NotNullExpression, NotNullExpression, UnknownNoOrigin]);

    // The column through the scan is u.id of a LATERAL subquery on the side a join extends, which the scanned plan
    // prints as (u.id), a parameter or a placeholder, beside a plain u.id of the side the join keeps: the server
    // returns NULL for it, and the plain reference must not decide it.  Each first column may be NULL.
    [Theory]
    [InlineData(
        "WITH c AS MATERIALIZED (SELECT t.y, u.id AS z FROM users u LEFT JOIN LATERAL "
            + "(SELECT u.id AS y FROM orders o WHERE o.user_id = u.id) t ON true) SELECT c.y FROM c"
    )]
    [InlineData(
        "WITH c AS MATERIALIZED (SELECT t.nm, u.name FROM users u LEFT JOIN LATERAL "
            + "(SELECT u.name AS nm FROM orders o WHERE o.user_id = u.id LIMIT 1) t ON true) SELECT c.nm FROM c"
    )]
    [InlineData(
        "SELECT s.y, s.z FROM (SELECT t.y, u.id AS z, row_number() OVER (PARTITION BY u.id) AS rn FROM users u "
            + "LEFT JOIN LATERAL (SELECT u.id AS y FROM orders o WHERE o.user_id = u.id) t ON true) s WHERE s.rn = 1"
    )]
    [InlineData(
        "SELECT s.y FROM (SELECT t.y FROM users u LEFT JOIN LATERAL "
            + "(SELECT u.id AS y FROM orders o WHERE o.user_id = u.id LIMIT 1) t ON true ORDER BY u.id LIMIT 10) s "
            + "WHERE s.y IS DISTINCT FROM 0"
    )]
    [InlineData(
        "SELECT s.y, o.id FROM orders o LEFT JOIN users u ON u.id = o.user_id AND u.deleted_at IS NOT NULL "
            + "CROSS JOIN LATERAL (SELECT u.id AS y, u2.id AS z FROM users u2 WHERE u2.id = o.user_id LIMIT 5) s "
            + "WHERE s.z > 0"
    )]
    public async Task DescribeAsync_SubqueryColumnThatTheScannedPlanPrintsInParentheses_IsNeverFalse(string sql) =>
        (await DescribedAsync(sql)).Answers[0].Nullable.ShouldNotBe(false);

    // A relation the planner proves empty under an outer join is a Result, whose columns print bare in a plan of
    // several relations: an expression of them is NULL on every row.  PostgreSQL 18 proves the third one empty from
    // the NOT NULL of placed_at; 14 does not, and prints o.id.
    [Theory]
    [InlineData(
        "SELECT CASE WHEN u.id > 0 THEN b.name ELSE 'n' END AS y, u.id FROM users u "
            + "LEFT JOIN (SELECT * FROM users WHERE false) b ON u.id = b.id"
    )]
    [InlineData("SELECT coalesce(b.id, b.id) AS y, u.id FROM users u LEFT JOIN users b ON false")]
    [InlineData(
        "SELECT coalesce(o.id, o.user_id) AS z, u.id FROM users u "
            + "LEFT JOIN orders o ON o.user_id = u.id AND o.placed_at IS NULL"
    )]
    [InlineData(
        "UPDATE orders o SET note = o.note FROM (SELECT 1 AS one) x LEFT JOIN users b ON false "
            + "RETURNING coalesce(b.id, b.id) AS bid, o.id"
    )]
    public async Task DescribeAsync_ExpressionOfARelationProvedEmpty_IsNeverFalse(string sql) =>
        (await DescribedAsync(sql)).Answers[0].Nullable.ShouldNotBe(false);
}

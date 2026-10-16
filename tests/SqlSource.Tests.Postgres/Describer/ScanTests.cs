using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// What part B of the walk answers against a real server: a column through a scan of another plan, a partitioned
// table, a foreign table and a root that is not the scan, and an expression the walk follows to the node that computes
// it; and the shapes near them that keep what they had.
[Collection(UsesPostgres.Name)]
public sealed class ScanTests(PostgresContainer postgres)
{
    // A partitioned table and a foreign table of these tests alone, made by SQL that can be run again, and left: the
    // container is thrown away.  The foreign table is orders, read back through postgres_fdw from this very server.
    private const string Partitioned = """
        CREATE TABLE IF NOT EXISTS scan_parts (id integer NOT NULL, kind integer NOT NULL, note text)
            PARTITION BY LIST (kind);
        CREATE TABLE IF NOT EXISTS scan_parts_1 PARTITION OF scan_parts FOR VALUES IN (1);
        CREATE TABLE IF NOT EXISTS scan_parts_2 PARTITION OF scan_parts FOR VALUES IN (2);
        """;

    private static readonly (bool?, string?) Catalog = (false, "catalog");

    private static readonly (bool?, string?) Nullable = (true, "catalog");

    private static readonly (bool?, string?) OuterJoin = (true, "outer-join");

    private static readonly (bool?, string?) Heuristic = (true, "heuristic");

    private static readonly (bool?, string?) Expression = (false, "expression");

    private static readonly (bool?, string?) Unknown = (null, "no-origin");

    private async Task<(bool? Nullable, string? Source)[]> WalkedAsync(string sql)
    {
        var (plan, answers) = await DescribedAsync(sql);

        plan.ShouldBe(SidecarValues.Plan.Walked);
        return answers;
    }

    private async Task<(string? Plan, (bool? Nullable, string? Source)[] Answers)> DescribedAsync(string sql)
    {
        var described = await Sessions.DescribeAsync(postgres.ConnectionString, Requests.Of(sql));

        described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
        var description = described.Value.ShouldNotBeNull();
        return (
            description.Plan,
            [.. description.Columns.ShouldNotBeNull().Select(static column => (column.Nullable, column.NullableSource))]
        );
    }

    private async Task RunAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // The row-number dedupe: the subquery is planned apart, under a Subquery Scan, and its LEFT JOIN extends orders.
    [Fact]
    public async Task Describe_ColumnsThroughASubqueryScan_AreAnsweredByTheirOrigins() =>
        (
            await WalkedAsync(
                "SELECT s.id, s.name, s.total FROM (SELECT u.id, u.name, o.total, "
                    + "row_number() OVER (PARTITION BY u.id ORDER BY o.placed_at DESC) AS rn "
                    + "FROM users u LEFT JOIN orders o ON o.user_id = u.id) s WHERE s.rn = 1 ORDER BY s.name"
            )
        ).ShouldBe([Catalog, Catalog, OuterJoin]);

    // Matched by name, a column renamed to another's name would be read as the other.
    [Fact]
    public async Task Describe_SubqueryColumnsRenamedToEachOthersNames_AreNotReadCrossed() =>
        (
            await WalkedAsync(
                "SELECT s.total, s.note FROM (SELECT o.note AS total, o.total AS note, row_number() OVER () AS n "
                    + "FROM orders o LEFT JOIN users u ON u.id = o.user_id) s WHERE s.n > 1"
            )
        ).ShouldBe([Nullable, Catalog]);

    // Both columns are users.id, of the side the join keeps and of the side it extends: which is which through the
    // scan cannot be told.
    [Fact]
    public async Task Describe_SubqueryColumnsOfASelfJoin_KeepTheHeuristic() =>
        (
            await WalkedAsync(
                "SELECT s.a, s.b FROM (SELECT u.id AS a, v.id AS b, row_number() OVER () AS n "
                    + "FROM users u LEFT JOIN users v ON v.id = u.id + 1) s WHERE s.n > 0"
            )
        ).ShouldBe([Heuristic, Heuristic]);

    // One plan of the CTE, and two scans of it, the second on the side the join extends.
    [Fact]
    public async Task Describe_ColumnsOfACteUsedTwice_AreAnsweredForEachScan() =>
        (
            await WalkedAsync(
                "WITH recent AS MATERIALIZED (SELECT o.id, o.user_id, o.note FROM orders o) "
                    + "SELECT r.id, r.note, n.id AS later FROM recent r "
                    + "LEFT JOIN recent n ON n.user_id = r.user_id AND n.id > r.id"
            )
        ).ShouldBe([Catalog, Nullable, OuterJoin]);

    // A condition on a parameter prunes at executor start, so the plan keeps the Append of every partition.
    [Fact]
    public async Task Describe_PartitionedTableOnEachSideOfALeftJoin_IsAnsweredByItsSide()
    {
        await RunAsync(Partitioned);

        (
            await WalkedAsync(
                "SELECT p.id, p.note, u.name FROM scan_parts p LEFT JOIN users u ON u.id = p.id WHERE p.kind = @kind"
            )
        ).ShouldBe([Catalog, Nullable, OuterJoin]);
        (
            await WalkedAsync("SELECT u.id, p.id FROM users u LEFT JOIN scan_parts p ON p.id = u.id AND p.kind = @kind")
        ).ShouldBe([Catalog, OuterJoin]);
    }

    // A partitioned table is no set operation: an aggregate over it is answered.
    [Fact]
    public async Task Describe_AggregateOverAPartitionedTable_IsAnswered()
    {
        await RunAsync(Partitioned);

        (await WalkedAsync("SELECT count(*) AS n, 'p' AS k FROM scan_parts p WHERE p.kind = @kind")).ShouldBe([
            Expression,
            Expression,
        ]);
    }

    // A UNION ALL of two partitions of one table whose branches differ is a set operation, whose root prints the
    // first branch: NULL in the second branch, the column stays unknown, whatever the plan.
    [Theory]
    [InlineData(
        "SELECT y.v FROM (SELECT x.kind AS v FROM scan_parts_1 x UNION ALL SELECT NULL::integer FROM scan_parts_2 x) y"
    )]
    [InlineData(
        "SELECT greatest(y.v, y.w) FROM (SELECT x.kind AS v, x.id AS w FROM scan_parts_1 x "
            + "UNION ALL SELECT NULL::integer, NULL::integer FROM scan_parts_2 x) y"
    )]
    public async Task Describe_UnionAllOfTwoPartitionsWithOtherBranches_StaysUnknown(string sql)
    {
        await RunAsync(Partitioned);

        (await DescribedAsync(sql)).Answers.ShouldBe([Unknown]);
    }

    // A foreign table's scan is an instance: unknown where nothing extends it, nullable where a join does.
    [Fact]
    public async Task Describe_ForeignTableOnEachSideOfALeftJoin_IsAnsweredByItsSide()
    {
        var connection = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        await RunAsync(
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
            (null, "foreign-table"),
            OuterJoin,
        ]);
        (await WalkedAsync("SELECT u.id, f.id FROM users u LEFT JOIN scan_foreign f ON f.id = u.id")).ShouldBe([
            Catalog,
            OuterJoin,
        ]);
    }

    // A plan of one relation prints bare names at a Limit over a Sort over the scan.  The function's name gates it.
    [Fact]
    public async Task Describe_BareNamesAtARootOverOneScan_AreTheRelations() =>
        (
            await WalkedAsync(
                "SELECT id, name, deleted_at FROM users WHERE left(name, 1) = @initial ORDER BY name LIMIT 10"
            )
        ).ShouldBe([Catalog, Catalog, Nullable]);

    // A value the root computes over a join that extends is answered: a COALESCE with a NOT NULL argument of the side
    // a RIGHT JOIN keeps, whose table a plain column names, and a constant.  A constant of a subquery pulled up from
    // the side a LEFT JOIN extends is computed at its scan, and stays unknown.
    [Fact]
    public async Task Describe_ExpressionTheRootComputesOverAnOuterJoin_IsAnswered() =>
        (
            await WalkedAsync(
                "SELECT o.id, coalesce(a.id, o.user_id) AS uid, 'k' AS k, s.c FROM users a "
                    + "RIGHT JOIN orders o ON o.user_id = a.id "
                    + "LEFT JOIN (SELECT o2.id, 'x'::text AS c FROM orders o2) s ON s.id = o.id"
            )
        ).ShouldBe([Catalog, Expression, Expression, Unknown]);
}

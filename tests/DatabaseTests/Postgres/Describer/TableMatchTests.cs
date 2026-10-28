using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// The table match against a real server: what only a server can show, the origins of a row description for
// SELECT *, for a view, for a RETURNING list and for a table that lost a column.
[Collection(UsesPostgres.Name)]
public sealed class TableMatchTests(PostgresDatabases postgres)
{
    private Task<QueryDescription> ShouldDescribeAsync(string sql) =>
        Sessions.ShouldDescribeAsync(postgres.ConnectionString, sql);

    private async Task ShouldMatchAsync(string sql, string table, string schema = "public")
    {
        var description = await ShouldDescribeAsync(sql);

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.MatchesTable.ShouldBe(new SidecarTable(schema, table));
    }

    // The whole of users, by * and by its list in order.
    [Theory]
    [InlineData("SELECT * FROM users WHERE id = @id")]
    [InlineData("SELECT id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users")]
    public Task DescribeAsync_WholeTableByStarOrByItsListInOrder_MatchesTheTable(string sql) =>
        ShouldMatchAsync(sql, "users");

    [Fact]
    public Task DescribeAsync_StarOfAView_MatchesTheView() =>
        ShouldMatchAsync("SELECT * FROM active_users", "active_users");

    [Fact]
    public Task DescribeAsync_StarOfATableInAnotherSchema_SaysItsSchema() =>
        ShouldMatchAsync("SELECT * FROM billing.invoices", "invoices", "billing");

    // A RETURNING list keeps the table's origins, so the row an INSERT gives back is the table's.
    [Fact]
    public Task DescribeAsync_InsertReturningStar_MatchesTheTable() =>
        ShouldMatchAsync("INSERT INTO orders (user_id, total) VALUES (@user_id, @total) RETURNING *", "orders");

    // The dropped attribute keeps its number and is not in the answer of "tables": the table's list is what is left.
    [Fact]
    public async Task DescribeAsync_StarOfATableWithADroppedColumn_MatchesTheTable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(postgres.ConnectionString);
        await using (var create = source.CreateCommand(ScratchSchema.DroppedColumn))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        // The premise: the table holds an attribute that is dropped.
        await using (
            var dropped = source.CreateCommand(
                "SELECT count(*) FROM pg_catalog.pg_attribute "
                    + "WHERE attrelid = 'match_dropped'::regclass AND attnum > 0 AND attisdropped"
            )
        )
        {
            (await dropped.ExecuteScalarAsync(token)).ShouldBe(1L);
        }

        await ShouldMatchAsync("SELECT * FROM match_dropped", "match_dropped");
    }

    // Each query differs from users in one way, and SELECT id, name AS user_name FROM users in two, and says the
    // first check that failed.
    [Theory]
    [InlineData("SELECT *, upper(name) AS shouted FROM users", "no-origin")]
    [InlineData("SELECT u.*, o.* FROM users AS u JOIN orders AS o ON o.user_id = u.id", "several-tables")]
    [InlineData(
        "SELECT id, name AS user_name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "names-differ"
    )]
    [InlineData(
        "SELECT id, name AS \"name!\", email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "names-differ"
    )]
    [InlineData("SELECT id, name AS user_name FROM users", "names-differ")]
    [InlineData("SELECT id, name, email, status, tags, balance, created_at, deleted_at FROM users", "columns-differ")]
    [InlineData(
        "SELECT id, id, name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "columns-differ"
    )]
    [InlineData("SELECT a.*, b.* FROM users AS a JOIN users AS b ON b.id = a.id", "columns-differ")]
    [InlineData(
        "SELECT name, id, email, status, tags, balance, created_at, deleted_at, name_upper FROM users",
        "order-differs"
    )]
    [InlineData("SELECT u.* FROM orders AS o LEFT JOIN users AS u ON u.id = o.user_id", "nullability-differs")]
    public async Task DescribeAsync_ResultThatIsNotATable_MatchesNothingAndSaysTheFirstCheckThatFailed(
        string sql,
        string expected
    )
    {
        var description = await ShouldDescribeAsync(sql);

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(expected);
    }
}

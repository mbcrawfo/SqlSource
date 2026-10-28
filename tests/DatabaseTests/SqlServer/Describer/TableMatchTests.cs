using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The table match against a real server, each query described in a session of its own: the origins the server gives
// under browse mode 2, and its description of SELECT * from the origin's table or view, which is the table's list.
[Collection(UsesSqlServer.Name)]
public sealed class TableMatchTests(SqlServerDatabases sqlServer)
{
    // The whole of users, by * and by its list in order: the computed columns and the rowversion are in its list, and
    // name_len_p, PERSISTED NOT NULL, is not null in the query, read from its one table, as in the table read whole.
    [Theory]
    [InlineData("SELECT * FROM dbo.users WHERE id = @id")]
    [InlineData(
        "SELECT id, name, email, nickname, credit, created_at, deleted_at, name_len, name_len_p, rv FROM dbo.users"
    )]
    public Task DescribeAsync_WholeTableByStarOrByItsListInOrder_MatchesTheTable(string sql) =>
        ShouldMatchAsync(sql, "dbo", "users");

    // Origins come from the catalog, so a name written in another case in a database whose collation ignores case
    // still matches, under the catalog's names.
    [Fact]
    public Task DescribeAsync_QueryNamesTableInOtherCase_Matches() =>
        ShouldMatchAsync("SELECT * FROM DBO.USERS", "dbo", "users");

    // Under browse mode 2 a view is its columns' origin.
    [Fact]
    public Task DescribeAsync_StarOfAView_MatchesTheView() =>
        ShouldMatchAsync("SELECT * FROM dbo.user_orders", "dbo", "user_orders");

    [Fact]
    public Task DescribeAsync_StarOfATableInAnotherSchema_SaysItsSchema() =>
        ShouldMatchAsync("SELECT * FROM billing.invoices", "billing", "invoices");

    // The period columns are HIDDEN: SELECT * leaves them out of the query and out of the table's list alike.
    [Fact]
    public Task DescribeAsync_StarOfATableWithHiddenPeriodColumns_MatchesWithoutThem() =>
        ShouldMatchAsync("SELECT * FROM dbo.prices", "dbo", "prices");

    // An OUTPUT list keeps the table's origins, so the row an INSERT gives back is the table's.
    [Fact]
    public Task DescribeAsync_InsertOutputStar_MatchesTheTable() =>
        ShouldMatchAsync(
            "INSERT INTO dbo.orders (user_id, total) OUTPUT inserted.* VALUES (@user_id, @total)",
            "dbo",
            "orders"
        );

    // Each query but the two of FOR SYSTEM_TIME and of another database, the two of a function, and the one of users,
    // differs from orders, id, user_id, total, note, in one way, and the last in three, and says the first check that
    // failed.  Under FOR SYSTEM_TIME the server gives no column an origin, and a column of another database or of a
    // function, inline or multi-statement, has none.  The one of users reads every column of users through a join,
    // which no proof follows, so name_len_p keeps the server's answer while the table's is corrected.
    [Theory]
    [InlineData(
        "SELECT id, user_id, total, note, total + 1 AS more FROM dbo.orders",
        SidecarValues.TableMatch.NoOrigin
    )]
    [InlineData("SELECT id, amount FROM dbo.prices FOR SYSTEM_TIME ALL", SidecarValues.TableMatch.NoOrigin)]
    [InlineData("SELECT database_id, name FROM master.sys.databases", SidecarValues.TableMatch.NoOrigin)]
    [InlineData("SELECT * FROM dbo.orders_of(@user_id)", SidecarValues.TableMatch.NoOrigin)]
    [InlineData("SELECT * FROM dbo.orders_table_of(@user_id)", SidecarValues.TableMatch.NoOrigin)]
    [InlineData(
        "SELECT o.id, o.user_id, o.total, u.name FROM dbo.orders AS o JOIN dbo.users AS u ON u.id = o.user_id",
        SidecarValues.TableMatch.SeveralTables
    )]
    [InlineData("SELECT id, user_id AS owner_id, total, note FROM dbo.orders", SidecarValues.TableMatch.NamesDiffer)]
    [InlineData("SELECT id, user_id, total, note AS [note!] FROM dbo.orders", SidecarValues.TableMatch.NamesDiffer)]
    [InlineData("SELECT id, user_id, total FROM dbo.orders", SidecarValues.TableMatch.ColumnsDiffer)]
    [InlineData("SELECT id, id, user_id, total, note FROM dbo.orders", SidecarValues.TableMatch.ColumnsDiffer)]
    [InlineData("SELECT user_id, id, total, note FROM dbo.orders", SidecarValues.TableMatch.OrderDiffers)]
    [InlineData(
        "SELECT o.id, o.user_id, o.total, o.note FROM dbo.users AS u LEFT JOIN dbo.orders AS o ON o.user_id = u.id",
        SidecarValues.TableMatch.NullabilityDiffers
    )]
    [InlineData(
        "SELECT u.id, u.name, u.email, u.nickname, u.credit, u.created_at, u.deleted_at, u.name_len, "
            + "u.name_len_p, u.rv FROM dbo.users AS u JOIN dbo.orders AS o ON o.user_id = u.id",
        SidecarValues.TableMatch.NullabilityDiffers
    )]
    [InlineData("SELECT user_id AS owner_id, id FROM dbo.orders", SidecarValues.TableMatch.NamesDiffer)]
    public async Task DescribeAsync_ResultThatIsNotATable_MatchesNothingAndSaysTheFirstCheckThatFailed(
        string sql,
        string expected
    )
    {
        var description = await ShouldDescribeAsync(sql);

        description.MatchesTable.ShouldBeNull();
        description.TableMatch.ShouldBe(expected);
    }

    private Task<QueryDescription> ShouldDescribeAsync(string sql) =>
        Sessions.ShouldDescribeAsync(sqlServer.ConnectionString, Of(SqlDialectRules.SqlServer, sql));

    private async Task ShouldMatchAsync(string sql, string schema, string table)
    {
        var description = await ShouldDescribeAsync(sql);

        description.TableMatch.ShouldBe(SidecarValues.TableMatch.Matched);
        description.MatchesTable.ShouldBe(new SidecarTable(schema, table));
        description.Plan.ShouldBe(SidecarValues.Plan.Skipped);
    }
}

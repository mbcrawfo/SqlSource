using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Describer;

// The oracle's runs against the server: a query that writes leaves the seed as it was, a column that held a NULL in a
// row is seen, a sample is bound to its parameter, of an alias type too and as an expression that ends in a comment,
// and a query that fails throws its own error.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticNullabilityOracleTests(SqlServerDatabases sqlServer)
{
    private const string CountStock = "SELECT COUNT_BIG(*) FROM dbo.stock";

    private const string CountCustomers = "SELECT COUNT(*) FROM dbo.customers";

    private const string CountActive = CountCustomers + " WHERE status = 'active'";

    // Msg 8134, "Divide by zero error encountered."
    private const int DivideByZero = 8134;

    [Fact]
    public async Task RunAsync_InsertOutput_LeavesTheTableAsItWas()
    {
        // Product 6, a gift card, is the one product with no stock row.
        const string Insert = """
            INSERT INTO dbo.stock (product_id, on_hand, updated_at)
            OUTPUT inserted.product_id
            SELECT p.id, 0, '2024-10-21 06:00:00'
            FROM dbo.products AS p
            WHERE NOT EXISTS (SELECT 1 FROM dbo.stock AS s WHERE s.product_id = p.id)
            """;
        var connection = sqlServer.RealisticConnectionString;
        var before = (long)(await RealisticDatabase.ScalarAsync(connection, CountStock))!;
        var batch = RealisticSamples.Batch(Insert, RealisticSample.Of(), []);

        var first = await RealisticNullabilityOracle.RunAsync(connection, batch);
        var second = await RealisticNullabilityOracle.RunAsync(connection, batch);

        ((long)(await RealisticDatabase.ScalarAsync(connection, CountStock))!).ShouldBe(before);
        first.Rows.ShouldBe(1);
        second.Rows.ShouldBe(1);
        first.Names.ShouldBe(["product_id"]);
        first.SawNull.ShouldBe([false]);
    }

    [Fact]
    public async Task RunAsync_NullableColumn_SawNullWhereARowHasOne()
    {
        var run = await RealisticNullabilityOracle.RunAsync(
            sqlServer.RealisticConnectionString,
            RealisticSamples.Batch("SELECT id, email FROM dbo.customers ORDER BY id", RealisticSample.Of(), [])
        );

        run.Names.ShouldBe(["id", "email"]);
        run.SawNull.ShouldBe([false, true]);
    }

    [Fact]
    public async Task RunAsync_Parameter_IsBoundToItsSample()
    {
        var connection = sqlServer.RealisticConnectionString;
        var active = (int)(await RealisticDatabase.ScalarAsync(connection, CountActive))!;
        var batch = RealisticSamples.Batch(
            "SELECT id, status FROM dbo.customers WHERE status = @status",
            RealisticSample.Of(("status", "'active'")),
            [new DescribedParameter("status", new SqlServerType("varchar(10)", 10, 0, 0, null), null)]
        );

        var run = await RealisticNullabilityOracle.RunAsync(connection, batch);

        run.Names.ShouldBe(["id", "status"]);
        run.Rows.ShouldBe(active);
        active.ShouldBeLessThan(
            (int)(await RealisticDatabase.ScalarAsync(connection, CountCustomers))!,
            "the seed has customers that are not active"
        );
    }

    [Fact]
    public async Task RunAsync_AliasTypedParameter_IsDeclaredAsItsTypeAndBound()
    {
        // dbo.customers.email is of the alias type dbo.Email, over nvarchar(254), and Ana Ruiz, customer 1, has this
        // address.
        const string AnaEmail = "N'ana.ruiz@example.com'";
        var connection = sqlServer.RealisticConnectionString;
        var email = new SqlServerType("nvarchar(254)", 508, 0, 0, new SqlServerUserType("dbo", "Email", null));
        var batch = RealisticSamples.Batch(
            "SELECT id FROM dbo.customers WHERE email = @email ORDER BY id",
            RealisticSample.Of(("email", AnaEmail)),
            [new DescribedParameter("email", email, null)]
        );

        var run = await RealisticNullabilityOracle.RunAsync(connection, batch);

        batch.Text.ShouldStartWith("DECLARE @sample_0 [dbo].[Email] = (");
        batch.Declarations.ShouldBe("@email [dbo].[Email]");
        run.Names.ShouldBe(["id"]);
        run.Rows.ShouldBe(1);
        (
            (int)(await RealisticDatabase.ScalarAsync(connection, $"{CountCustomers} WHERE email = {AnaEmail}"))!
        ).ShouldBe(1, "Ana Ruiz alone has the address");
    }

    [Fact]
    public async Task RunAsync_SampleThatIsAnExpressionWithAComment_IsBound()
    {
        // Thirty days before 2024-10-14 21:15, when order 15, the seed's latest, was placed: 2024-09-14 21:15.
        const string Since = """
            DATEADD(day, -30, CAST('2024-10-14T21:15:00' AS datetime2(0))) -- thirty days before order 15
            """;
        const string CountSince = "SELECT COUNT(*) FROM dbo.orders WHERE placed_at >= '2024-09-14T21:15:00'";
        var connection = sqlServer.RealisticConnectionString;
        var batch = RealisticSamples.Batch(
            "SELECT id, placed_at FROM dbo.orders WHERE placed_at >= @since ORDER BY placed_at",
            RealisticSample.Of(("since", Since)),
            [new DescribedParameter("since", new SqlServerType("datetime2(0)", 6, 19, 0, null), null)]
        );
        var expected = (int)(await RealisticDatabase.ScalarAsync(connection, CountSince))!;

        var run = await RealisticNullabilityOracle.RunAsync(connection, batch);

        run.Names.ShouldBe(["id", "placed_at"]);
        run.Rows.ShouldBe(expected);
        expected.ShouldBe(4, "orders 12 to 15 were placed on or after 2024-09-14 21:15");
    }

    [Fact]
    public async Task RunAsync_QueryThatFails_ThrowsTheQuerysError()
    {
        var thrown = await Should.ThrowAsync<SqlException>(() =>
            RealisticNullabilityOracle.RunAsync(
                sqlServer.RealisticConnectionString,
                RealisticSamples.Batch("SELECT 1/0 AS x", RealisticSample.Of(), [])
            )
        );

        thrown.Number.ShouldBe(DivideByZero);
    }
}

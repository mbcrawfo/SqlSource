using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic.Describer;

// The oracle's runs against the server: a query that writes leaves the seed as it was, a column that held a NULL in a
// row is seen, a sample is bound to its parameter, and a query that fails throws its own error.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticNullabilityOracleTests(SqlServerContainer sqlServer)
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
        var batch = RealisticSamples.Batch(Insert, Values(), []);

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
            RealisticSamples.Batch("SELECT id, email FROM dbo.customers ORDER BY id", Values(), [])
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
            Values(("status", "'active'")),
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
    public async Task RunAsync_QueryThatFails_ThrowsTheQuerysError()
    {
        var thrown = await Should.ThrowAsync<SqlException>(() =>
            RealisticNullabilityOracle.RunAsync(
                sqlServer.RealisticConnectionString,
                RealisticSamples.Batch("SELECT 1/0 AS x", Values(), [])
            )
        );

        thrown.Number.ShouldBe(DivideByZero);
    }

    private static Dictionary<string, string> Values(params (string Name, string Value)[] values)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            found.Add(name, value);
        }

        return found;
    }
}

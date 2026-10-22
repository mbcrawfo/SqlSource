using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.Realistic;

// The database "realistic" of the corpus: made beside "fixtures", with its schema and its seed applied, and the
// shared database left as it was.
[Collection(UsesSqlServer.Name)]
public sealed class RealisticDatabaseTests(SqlServerContainer sqlServer)
{
    // Every user table of schema.sql, the history of a system-versioned table among them, and dbo.product_specs on a
    // server that has it.
    private const string Tables = """
        SELECT QUOTENAME(SCHEMA_NAME(schema_id)) + N'.' + QUOTENAME(name)
        FROM sys.tables
        WHERE SCHEMA_NAME(schema_id) IN (N'dbo', N'billing') AND is_ms_shipped = 0
        ORDER BY 1
        """;

    [Fact]
    public async Task Database_AfterStart_HasRowsInEveryTableAndLeavesTheFixturesAlone()
    {
        var realistic = sqlServer.RealisticConnectionString;

        var tables = await TablesAsync(realistic);
        var sharedUntouched = await RealisticDatabase.ScalarAsync(
            sqlServer.ConnectionString,
            "SELECT CAST(CASE WHEN OBJECT_ID(N'dbo.customers') IS NULL THEN 1 ELSE 0 END AS bit)"
        );

        tables.ShouldContain("[billing].[invoices]");
        tables.ShouldContain("[dbo].[prices_history]");
        foreach (var table in tables)
        {
            var rows = await RealisticDatabase.ScalarAsync(realistic, $"SELECT COUNT_BIG(*) FROM {table}");
            ((long)rows!).ShouldBeGreaterThan(0, $"{table} has no rows");
        }

        ((bool)sharedUntouched!).ShouldBeTrue();
    }

    private static async Task<List<string>> TablesAsync(string connection)
    {
        var token = TestContext.Current.CancellationToken;
        await using var opened = new SqlConnection(connection);
        await opened.OpenAsync(token);
        await using var command = new SqlCommand(Tables, opened);
        await using var reader = await command.ExecuteReaderAsync(token);
        var tables = new List<string>();
        while (await reader.ReadAsync(token))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}

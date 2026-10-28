using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic;

// The database "realistic", as a test of the corpus asks it a question of its own.
internal static class RealisticDatabase
{
    // The one value of a query, on a connection of its own.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a test's own query of the database it set up."
    )]
    public static async Task<object?> ScalarAsync(string connection, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var opened = new SqlConnection(connection);
        await opened.OpenAsync(token);
        await using var command = new SqlCommand(sql, opened);
        return await command.ExecuteScalarAsync(token);
    }
}

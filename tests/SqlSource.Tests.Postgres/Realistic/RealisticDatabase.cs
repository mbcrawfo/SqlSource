using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Npgsql;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

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
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}

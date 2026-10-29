using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic.Describer;

// The oracle of the describer's nullability: executes a case's query over the seed, independent of what the describer
// recorded of it, and says which columns held a NULL.
internal static class RealisticNullabilityOracle
{
    // Reads every row, in a transaction that is rolled back, so that a query that writes leaves the seed as it was.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a case's own query, from the repository, with the case's own sample literals."
    )]
    public static async Task<NullabilityRun> RunAsync(string connection, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var opened = new NpgsqlConnection(connection);
        await opened.OpenAsync(token);
        await using var transaction = await opened.BeginTransactionAsync(token);
        NullabilityRun run;
        try
        {
            await using var command = new NpgsqlCommand(sql, opened, transaction);
            await using var reader = await command.ExecuteReaderAsync(token);
            var names = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToList();
            var sawNull = new bool[reader.FieldCount];
            var rows = 0;
            while (await reader.ReadAsync(token))
            {
                rows++;
                for (var ordinal = 0; ordinal < sawNull.Length; ordinal++)
                {
                    sawNull[ordinal] |= await reader.IsDBNullAsync(ordinal, token);
                }
            }

            run = new NullabilityRun(names, sawNull, rows);
        }
        catch
        {
            await RollBackAfterAsync(transaction);
            throw;
        }

        // Not the test's token: a cancelled test still rolls back what its query wrote.
        await transaction.RollbackAsync(CancellationToken.None);
        return run;
    }

    // The rollback of a query that failed, which fails too when the connection broke.  RealisticMethodTests rolls back
    // a method that failed by it too.
    public static async Task RollBackAfterAsync(NpgsqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollback) when (rollback is NpgsqlException or InvalidOperationException)
        {
            // The query's own error is the one that says why, and the server rolls back what a broken connection
            // wrote.
        }
    }
}

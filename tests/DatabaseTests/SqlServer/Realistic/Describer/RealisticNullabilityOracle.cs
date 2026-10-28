using System;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Realistic.Describer;

// The oracle of the describer's nullability: executes a case's query over the seed, independent of what the describer
// recorded of it, and says which columns held a NULL.
internal static class RealisticNullabilityOracle
{
    // Runs the batch of RealisticSamples, with the query's text and its declarations as parameters of their own, and
    // reads every row of its first result set, as the describer describes the first.  It runs in a transaction that is
    // rolled back, so that a query that writes leaves the seed as it was.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a case's own query, from the repository, with the case's own sample literals."
    )]
    public static async Task<NullabilityRun> RunAsync(string connection, SampleBatch batch)
    {
        var token = TestContext.Current.CancellationToken;
        await using var opened = new SqlConnection(connection);
        await opened.OpenAsync(token);
        await using var transaction = (SqlTransaction)await opened.BeginTransactionAsync(token);
        NullabilityRun run;
        try
        {
            await using var command = new SqlCommand(batch.Text, opened, transaction);
            _ = command.Parameters.Add(Text("@statement", batch.Statement));
            if (batch.Declarations.Length > 0)
            {
                _ = command.Parameters.Add(Text("@declarations", batch.Declarations));
            }

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

    // A parameter of sp_executesql, which takes its statement and its declarations as nvarchar(max).
    private static SqlParameter Text(string name, string value) => new(name, SqlDbType.NVarChar, -1) { Value = value };

    // The rollback of a query that failed, which fails too when the error ended the transaction or the connection.
    // RealisticMethodTests rolls back a method that failed by it too.
    public static async Task RollBackAfterAsync(SqlTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch (Exception rollback) when (rollback is SqlException or InvalidOperationException)
        {
            // The query's own error is the one that says why, and the server rolls back what a broken connection
            // wrote.
        }
    }
}

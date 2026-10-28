using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// A transaction of a session under SET XACT_ABORT ON, for the tests of an error that SqlClient does not report: under
// XACT_ABORT the error rolls the transaction back, and nothing is thrown.  The connection is not pooled, so the setting
// ends with its session when the test disposes it.  The temporary table #outside is made before the transaction, so it
// outlives a rollback and holds what was committed: 7 is written in the transaction before the method is called, and
// 8 with the transaction after it.
internal sealed class XactAbortTransaction : IAsyncDisposable
{
    private XactAbortTransaction(SqlConnection connection, SqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    public SqlConnection Connection { get; }

    public SqlTransaction Transaction { get; }

    public static async Task<XactAbortTransaction> BeginAsync(SqlServerContainer sqlServer, CancellationToken token)
    {
        var connection = EndToEndSources.AppUnpooled(sqlServer);
        await connection.OpenAsync(token);
        await using (var create = new SqlCommand("CREATE TABLE #outside (n int); SET XACT_ABORT ON;", connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        await using (var insert = new SqlCommand("INSERT INTO #outside (n) VALUES (7)", connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        return new XactAbortTransaction(connection, transaction);
    }

    // What a caller sees after the method: the transaction has completed, so its Connection is null; a command given it
    // runs outside any transaction, with no error, and commits on its own; and only the commit throws.  The 7 that the
    // transaction wrote is gone and the 8 written after it stays.
    public async Task ShouldHaveRolledBackUnreportedAsync(CancellationToken token)
    {
        Transaction.Connection.ShouldBeNull();

        await using (var insert = new SqlCommand("INSERT INTO #outside (n) VALUES (8)", Connection, Transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => Transaction.CommitAsync(token));

        thrown.Message.ShouldBe("This SqlTransaction has completed; it is no longer usable.");
        await using var select = new SqlCommand("SELECT n FROM #outside ORDER BY n", Connection);
        await using var reader = await select.ExecuteReaderAsync(token);
        var committed = new List<int>();
        while (await reader.ReadAsync(token))
        {
            committed.Add(reader.GetInt32(0));
        }

        committed.ShouldBe([8]);
    }

    public async ValueTask DisposeAsync()
    {
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }
}

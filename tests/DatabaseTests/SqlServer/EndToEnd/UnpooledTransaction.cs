using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// A transaction of a session with XACT_ABORT on or off, for the tests of an error that SqlClient does not report and
// that may roll the transaction back with nothing thrown: under XACT_ABORT ON nearly any error does, and with it off
// some errors do, such as CAST('x' AS int), and others, such as an overflow, do not.  The session's setting is checked
// before the transaction begins, since CAST('x' AS int) rolls back under either setting and could not show which one
// the test ran under.  The connection is not pooled, so the
// setting ends with its session when the test disposes it.  The temporary table #outside is made before the
// transaction, so it outlives a rollback and holds what was committed: 7 is written in the transaction before the
// method is called, and 8 with the transaction after it.
internal sealed class UnpooledTransaction : IAsyncDisposable
{
    private UnpooledTransaction(SqlConnection connection, SqlTransaction transaction)
    {
        Connection = connection;
        Transaction = transaction;
    }

    public SqlConnection Connection { get; }

    public SqlTransaction Transaction { get; }

    public static async Task<UnpooledTransaction> BeginAsync(
        SqlServerContainer sqlServer,
        bool xactAbort,
        CancellationToken token
    )
    {
        var connection = EndToEndSources.AppUnpooled(sqlServer);
        await connection.OpenAsync(token);
        var setUp = xactAbort ? "CREATE TABLE #outside (n int); SET XACT_ABORT ON;" : "CREATE TABLE #outside (n int);";
        await using (var create = new SqlCommand(setUp, connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        // XACT_ABORT is the bit 16384 of @@OPTIONS; SESSIONPROPERTY does not know it, and returns NULL for it.
        const string XactAbortOfSession = "SELECT CASE WHEN @@OPTIONS & 16384 = 0 THEN 0 ELSE 1 END";
        await using (var setting = new SqlCommand(XactAbortOfSession, connection))
        {
            (await setting.ExecuteScalarAsync(token)).ShouldBe(xactAbort ? 1 : 0, "XACT_ABORT of the session");
        }

        var transaction = (SqlTransaction)await connection.BeginTransactionAsync(token);
        await using (var insert = new SqlCommand("INSERT INTO #outside (n) VALUES (7)", connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        return new UnpooledTransaction(connection, transaction);
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
        (await ReadAsync(select, token)).ShouldBe([8]);
    }

    // What a caller sees after the method when the error left the transaction open: its Connection is still set, the
    // commit succeeds, and #outside holds the 7 that the transaction wrote.
    public async Task ShouldCommitAsync(CancellationToken token)
    {
        _ = Transaction.Connection.ShouldNotBeNull();

        await Transaction.CommitAsync(token);

        await using var select = new SqlCommand("SELECT n FROM #outside ORDER BY n", Connection);
        (await ReadAsync(select, token)).ShouldBe([7]);
    }

    public async ValueTask DisposeAsync()
    {
        await Transaction.DisposeAsync();
        await Connection.DisposeAsync();
    }

    private static async Task<List<int>> ReadAsync(SqlCommand select, CancellationToken token)
    {
        await using var reader = await select.ExecuteReaderAsync(token);
        var committed = new List<int>();
        while (await reader.ReadAsync(token))
        {
            committed.Add(reader.GetInt32(0));
        }

        return committed;
    }
}

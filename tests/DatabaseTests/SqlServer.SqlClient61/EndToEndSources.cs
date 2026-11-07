using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The helper that the tests share, as the end-to-end project's EndToEndSources has it.
internal static class EndToEndSources
{
    // Opens the connection and runs the write in a transaction that is rolled back.  The write gives the transaction to
    // every call, since SqlClient refuses a command on a connection whose transaction it is not given.  Disposing the
    // transaction rolls it back, and SqlClient skips one that the server has already ended, where a rollback of its own
    // in a finally would throw and take the place of the write's exception.
    public static async Task RolledBackAsync(
        SqlConnection connection,
        Func<DbTransaction, Task> write,
        CancellationToken cancellationToken
    )
    {
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await write(transaction);
    }
}

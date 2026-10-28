using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The mark of the queries that set SET CONTEXT_INFO before a later statement: the first byte of the session's context,
// which stays on an open connection and which the pool resets when the connection is returned to it.  A test clears it,
// runs the query on the same open connection, and reads it back to see how far the batch ran.
internal static class SessionMark
{
    public static async Task ClearAsync(SqlConnection connection, CancellationToken token)
    {
        await using var clear = new SqlCommand("SET CONTEXT_INFO 0x00", connection);
        _ = await clear.ExecuteNonQueryAsync(token);
    }

    public static async Task<object?> ReadAsync(SqlConnection connection, CancellationToken token)
    {
        await using var select = new SqlCommand("SELECT CAST(SUBSTRING(CONTEXT_INFO(), 1, 1) AS int)", connection);
        return await select.ExecuteScalarAsync(token);
    }
}

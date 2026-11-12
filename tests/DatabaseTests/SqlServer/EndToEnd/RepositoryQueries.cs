using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The three repositories of Queries/Repository.sql, whose header sets the output repository: each gets the file's
// queries as instance methods, asynchronous and synchronous, and the interface I<its name>, and differs only in where
// its methods take a connection.
// SqlClient has no data source, so a SQL Server repository has none.

// A connection string by convention, a string named connectionString: each method makes a SqlConnection of it, opens
// it and disposes it.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A pair of factories by the attribute, methods that take a CancellationToken, one returning a task for the
// asynchronous methods and one returning none for the synchronous ones: each method calls its own and disposes the
// connection it returns.  A factory opens each connection itself, so the method never closes one; a connection that
// is closed after the method was disposed by it.  Each keeps the connections it opened for a test to look at, with
// those whose Disposed event has been raised, the synchronous one also the token it was given, and, given no
// connection string, returns null, as a factory of the user's may.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class FactoryRepository(string? appConnectionString)
{
    public List<SqlConnection> Connections { get; } = [];

    public List<SqlConnection> Disposed { get; } = [];

    public List<SqlConnection> SyncConnections { get; } = [];

    public List<SqlConnection> SyncDisposed { get; } = [];

    public List<CancellationToken> SyncTokens { get; } = [];

    [SqlSourceConnection]
    private async ValueTask<SqlConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (appConnectionString is null)
        {
            return null;
        }

        var connection = new SqlConnection(appConnectionString);
        Connections.Add(connection);
        connection.Disposed += (_, _) => Disposed.Add(connection);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    [SqlSourceConnection]
    private SqlConnection? Open(CancellationToken cancellationToken)
    {
        if (appConnectionString is null)
        {
            return null;
        }

        var connection = new SqlConnection(appConnectionString);
        SyncConnections.Add(connection);
        SyncTokens.Add(cancellationToken);
        connection.Disposed += (_, _) => SyncDisposed.Add(connection);
        connection.Open();
        return connection;
    }
}

// A borrowed connection by the attribute, a DbConnection property: each method opens it when it is closed and closes it
// again, leaves an open one open, and never disposes it, which is the caller's.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class BorrowedRepository(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

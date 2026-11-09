using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The three repositories of Queries/Repository.sql, whose header sets the output repository: each gets the file's
// queries as instance methods and the interface I<its name>, and differs only in where its methods take a connection.
// SqlClient has no data source, so a SQL Server repository has none.

// A connection string by convention, a string named connectionString: each method makes a SqlConnection of it, opens
// it and disposes it.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A factory by the attribute, a method that takes a CancellationToken: each method calls it, and disposes the
// connection it returns.  It opens each connection, which the method then leaves open, so that one closed after the
// method was closed by its disposal, and keeps each, with those whose Disposed event has been raised, for a test to
// look at after the method.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class FactoryRepository(string appConnectionString)
{
    public List<SqlConnection> Connections { get; } = [];

    public List<SqlConnection> Disposed { get; } = [];

    [SqlSourceConnection]
    private async ValueTask<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(appConnectionString);
        Connections.Add(connection);
        connection.Disposed += (_, _) => Disposed.Add(connection);
        await connection.OpenAsync(cancellationToken);
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

using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The four repositories of Queries/Repository.sql, whose header sets the output repository: each gets the file's
// queries as instance methods and the interface I<its name>, and differs only in where its methods take a connection.

// A data source by convention, a primary-constructor parameter of a type derived from DbDataSource: each method opens a
// connection of it and disposes it.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class DataSourceRepository(NpgsqlDataSource dataSource);

// A connection string by convention, a string named connectionString: each method makes an NpgsqlConnection of it,
// opens it and disposes it.  It has none of the data source's mappings, which these queries do not need.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A factory by the attribute, a method that takes a CancellationToken: each method calls it, and disposes the
// connection it returns.  The factory opens each connection itself, so the method never closes one; a connection that
// is closed after the method was disposed by it.  It keeps each connection for a test to look at, and, given no data
// source, returns null, as a factory of the user's may.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class FactoryRepository(NpgsqlDataSource? dataSource)
{
    public List<NpgsqlConnection> Connections { get; } = [];

    [SqlSourceConnection]
    private async ValueTask<NpgsqlConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (dataSource is null)
        {
            return null;
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Connections.Add(connection);
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

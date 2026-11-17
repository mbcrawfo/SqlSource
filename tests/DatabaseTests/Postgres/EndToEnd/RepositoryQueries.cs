using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The six repositories of Queries/Repository.sql, whose header sets the output repository: each gets the file's
// queries as instance methods, asynchronous and synchronous, and the interface I<its name>, and differs only in where
// its methods take a connection.

// A data source by convention, a primary-constructor parameter of a type derived from DbDataSource: each method opens a
// connection of it and disposes it.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class DataSourceRepository(NpgsqlDataSource dataSource);

// A connection string by convention, a string named connectionString: each method makes an NpgsqlConnection of it,
// opens it and disposes it.  It has none of the data source's mappings, which these queries do not need.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A pair of factories by the attribute, methods that take a CancellationToken, one returning a task for the
// asynchronous methods and one returning none for the synchronous ones: each method calls its own and disposes the
// connection it returns.  A factory opens each connection itself, so the method never closes one; a connection that
// is closed after the method was disposed by it.  Each keeps the connections it opened for a test to look at, the
// synchronous one also the token it was given, and, given no data source, returns null, as a factory of the user's
// may.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class FactoryRepository(NpgsqlDataSource? dataSource)
{
    public List<NpgsqlConnection> Connections { get; } = [];

    public List<NpgsqlConnection> SyncConnections { get; } = [];

    public List<CancellationToken> SyncTokens { get; } = [];

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

    [SqlSourceConnection]
    private NpgsqlConnection? Open(CancellationToken cancellationToken)
    {
        if (dataSource is null)
        {
            return null;
        }

        var connection = dataSource.OpenConnection();
        SyncConnections.Add(connection);
        SyncTokens.Add(cancellationToken);
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

// A factory that the repository holds, the interface of a primary-constructor parameter: its marked methods, a pair of
// one asynchronous and one synchronous, are the connection sources of the repository that holds it, which calls them on
// the parameter and disposes the connection each returns.
internal interface IConnectionFactory
{
    [SqlSourceConnection]
    ValueTask<NpgsqlConnection?> OpenAsync(CancellationToken cancellationToken);

    [SqlSourceConnection]
    NpgsqlConnection? Open(CancellationToken cancellationToken);
}

// The factory of a data source, which keeps the connections it opened for a test to look at and, given none, returns
// null, as a factory of the user's may.
internal sealed class DataSourceConnectionFactory(NpgsqlDataSource? dataSource) : IConnectionFactory
{
    public List<NpgsqlConnection> Connections { get; } = [];

    public async ValueTask<NpgsqlConnection?> OpenAsync(CancellationToken cancellationToken)
    {
        if (dataSource is null)
        {
            return null;
        }

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Connections.Add(connection);
        return connection;
    }

    public NpgsqlConnection? Open(CancellationToken cancellationToken)
    {
        if (dataSource is null)
        {
            return null;
        }

        var connection = dataSource.OpenConnection();
        Connections.Add(connection);
        return connection;
    }
}

// A held factory by the parameter's type: each method calls the marked method of the factory it was given, since a
// factory the repository holds is a source of the repository as its own members are.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class HeldFactoryRepository(IConnectionFactory factory);

// No source of its own: a global, a static member marked in the project, serves it.  The global names the logical
// database of the file, "app", so that it serves no other repository of the project.
[SqlSourceGenerate(Source = "Queries/Repository.sql")]
internal sealed partial class GlobalRepository;

// The global of GlobalRepository, which a test sets to a data source of its own and sets back to null.
internal static class EndToEndConnections
{
    [SqlSourceConnection(Database = "app")]
    public static NpgsqlDataSource? Main { get; set; }
}

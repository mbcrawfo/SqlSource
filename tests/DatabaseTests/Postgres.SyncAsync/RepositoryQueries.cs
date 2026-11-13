using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// The repositories of Queries/Repository.sql, one for each source kind, each serving the synchronous methods and the
// asynchronous ones.

[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class DataSourceRepository(NpgsqlDataSource dataSource);

[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A pair of factories: the task for the asynchronous methods and the connection for the synchronous ones.
[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class FactoryRepository(NpgsqlDataSource dataSource)
{
    [SqlSourceConnection]
    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken) =>
        dataSource.OpenConnectionAsync(cancellationToken);

    [SqlSourceConnection]
    private NpgsqlConnection Open() => dataSource.OpenConnection();
}

[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class BorrowedRepository(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

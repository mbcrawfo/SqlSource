using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The repositories of Queries/Repository.sql, one for each source kind, each serving the synchronous methods and the
// asynchronous ones.

[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class ConnectionStringRepository(string connectionString);

// A pair of factories: the task for the asynchronous methods and the connection for the synchronous ones.
[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class FactoryRepository(string connectionString)
{
    [SqlSourceConnection]
    private async ValueTask<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    [SqlSourceConnection]
    private SqlConnection Open()
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}

[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Repository.sql")]
internal sealed partial class BorrowedRepository(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

using System.Data.Common;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// Batches.sql, whose repository queries borrow the connection a scenario gives.
[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Batches.sql")]
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

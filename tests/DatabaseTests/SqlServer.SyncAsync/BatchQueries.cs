using System.Data.Common;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// Batches.sql, whose repository queries borrow the connection a scenario gives.
[SqlSourceGenerate(Source = "obj/Mirror/EndToEnd/Queries/Batches.sql")]
#pragma warning disable CA1812 // Made only by a scenario's Instance, and no scenario of Batches.sql is written yet.
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

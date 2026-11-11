using System.Data.Common;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The collection methods, by the header of Queries/Batches.sql: extension methods of the connection in
// BatchQueriesExtensions, each beside its query method, and InsertNoteInRepository, at the output repository by its
// marker, an instance method of this type that runs on the borrowed connection, which it opens when it is closed and
// never disposes.
[SqlSourceGenerate(Source = "Queries/Batches.sql")]
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

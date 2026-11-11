using System.Data.Common;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The collection methods, by the header of Queries/Batches.sql: extension methods of the connection in
// BatchQueriesExtensions, each beside its query method, and InsertNoteInRepository, InsertNoteSerializableInRepository
// and InsertNoteDefaultLevelInRepository, at the output repository by their markers, instance methods of this type that
// run on the borrowed connection, which they open when it is closed and never dispose.
[SqlSourceGenerate(Source = "Queries/Batches.sql")]
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

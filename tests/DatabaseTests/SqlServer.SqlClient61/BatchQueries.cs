using System.Data.Common;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The collection methods, linked from the end-to-end project: extension methods of the connection in
// BatchQueriesExtensions under SqlClient 6.1, and InsertNoteInRepository, at the output repository, an instance method
// of this type that runs on the borrowed connection, as the end-to-end project's BatchQueries has it.
[SqlSourceGenerate(Source = "../SqlServer/EndToEnd/Queries/Batches.sql")]
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

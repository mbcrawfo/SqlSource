using System.Data.Common;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The collection methods, linked from the end-to-end project: extension methods of the connection in
// BatchQueriesExtensions under Npgsql 8, and InsertNoteInRepository, at the output repository, an instance method of
// this type that runs on the borrowed connection, as the end-to-end project's BatchQueries has it.
[SqlSourceGenerate(Source = "../Postgres/EndToEnd/Queries/Batches.sql")]
internal sealed partial class BatchQueries(DbConnection connection)
{
    [SqlSourceConnection]
    public DbConnection Connection { get; } = connection;
}

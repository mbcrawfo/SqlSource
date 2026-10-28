namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The streaming method, linked from the end-to-end project: its header gives every many query of the file an
// IAsyncEnumerable.  Extension methods of the connection in StreamingQueriesExtensions, under Npgsql 8.
[SqlSourceGenerate(Path = "../Postgres/EndToEnd/Queries/Streaming.sql")]
internal static partial class StreamingQueries;

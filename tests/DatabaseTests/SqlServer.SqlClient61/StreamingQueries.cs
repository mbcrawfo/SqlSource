namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The streaming method, linked from the end-to-end project: its header gives every many query of the file an
// IAsyncEnumerable.  Extension methods of the connection in StreamingQueriesExtensions, under SqlClient 6.1.
[SqlSourceGenerate(Path = "../SqlServer/EndToEnd/Queries/Streaming.sql")]
internal static partial class StreamingQueries;

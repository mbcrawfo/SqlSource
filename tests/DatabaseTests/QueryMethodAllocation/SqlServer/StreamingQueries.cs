namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The claim of the SQL Server end-to-end project's StreamingQueries, repeated so that the methods measured here are the
// ones that project runs: the streaming method, by a header marker that gives every many query of the file an
// IAsyncEnumerable, as extension methods of the connection in StreamingQueriesExtensions.
[SqlSourceGenerate(Path = "../../SqlServer/EndToEnd/Queries/Streaming.sql")]
internal static partial class StreamingQueries;

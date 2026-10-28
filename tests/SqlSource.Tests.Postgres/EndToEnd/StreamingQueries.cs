namespace SqlSource.Tests.Postgres.EndToEnd;

// The streaming method, by a header marker that gives every many query of the file an IAsyncEnumerable: extension
// methods of the connection in StreamingQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Streaming.sql")]
internal static partial class StreamingQueries;

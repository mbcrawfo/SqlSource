namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The claim of the PostgreSQL end-to-end project's StreamingQueries, repeated so that the methods measured here are the
// ones that project runs: the streaming method, by a header marker that gives every many query of the file an
// IAsyncEnumerable, as extension methods of the connection in StreamingQueriesExtensions.
[SqlSourceGenerate(Path = "../../Postgres/EndToEnd/Queries/Streaming.sql")]
internal static partial class StreamingQueries;

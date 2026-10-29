namespace SqlSource.DatabaseTests.QueryMethodAllocation.Postgres;

// The claim of the PostgreSQL end-to-end project's RowBufferQueries, repeated so that the methods measured here are
// the ones that project runs: the eight collection types that read their rows through the pooled row buffer, each read
// through it and under no-array-pool without it, as extension methods of the connection in RowBufferQueriesExtensions.
[SqlSourceGenerate(Path = "../../Postgres/EndToEnd/Queries/RowBuffers.sql")]
internal static partial class RowBufferQueries;

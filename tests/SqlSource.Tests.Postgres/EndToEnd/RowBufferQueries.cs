namespace SqlSource.Tests.Postgres.EndToEnd;

// The array and the read-only interfaces, each read through the pooled row buffer and under no-array-pool without it:
// extension methods of the connection in RowBufferQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/RowBuffers.sql")]
internal static partial class RowBufferQueries;

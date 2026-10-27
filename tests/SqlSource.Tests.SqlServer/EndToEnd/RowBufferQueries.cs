namespace SqlSource.Tests.SqlServer.EndToEnd;

// The eight collection types that read their rows through the pooled row buffer, each read through it and under
// no-array-pool without it: extension methods of the connection in RowBufferQueriesExtensions, the default location.
[SqlSourceGenerate(Path = "Queries/RowBuffers.sql")]
internal static partial class RowBufferQueries;

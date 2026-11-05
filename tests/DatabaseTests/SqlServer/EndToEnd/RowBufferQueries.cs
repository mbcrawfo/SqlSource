namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The eight collection types that read their rows through the pooled row buffer, each read through it and under
// array-pool=off without it: extension methods of the connection in RowBufferQueriesExtensions, the default location.
[SqlSourceGenerate(Source = "Queries/RowBuffers.sql")]
internal static partial class RowBufferQueries;

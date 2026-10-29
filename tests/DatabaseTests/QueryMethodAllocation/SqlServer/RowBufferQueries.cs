namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The claim of the SQL Server end-to-end project's RowBufferQueries, repeated so that the methods measured here are the
// ones that project runs: the eight collection types that read their rows through the pooled row buffer, each read
// through it and under no-array-pool without it, as extension methods of the connection in RowBufferQueriesExtensions,
// the default location.
[SqlSourceGenerate(Path = "../../SqlServer/EndToEnd/Queries/RowBuffers.sql")]
internal static partial class RowBufferQueries;

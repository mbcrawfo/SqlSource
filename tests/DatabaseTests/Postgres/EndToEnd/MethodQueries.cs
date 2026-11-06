namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The result shapes, rowcount, a token, the method suffix and a query that waits on a lock before its last row, by
// the project's settings:
// extension methods of the connection in MethodQueriesExtensions.
[SqlSourceGenerate(Source = "Queries/Methods.sql")]
internal static partial class MethodQueries;

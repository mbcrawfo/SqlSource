namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The result shapes, a token with its default, the counts of rowcount and the run-time arguments, with the methods in
// MethodQueriesExtensions, the default location.
[SqlSourceGenerate(Path = "Queries/Methods.sql")]
internal static partial class MethodQueries;

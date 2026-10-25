namespace SqlSource.Tests.SqlServer.EndToEnd;

// The result shapes, a token with its default and the counts of rowcount, with the methods in MethodQueriesExtensions,
// the default location.
[SqlSourceGenerate(Path = "Queries/Methods.sql")]
internal static partial class MethodQueries;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The result shapes, rowcount, a token, the method suffix and two queries that sleep, by the project's settings:
// extension methods of the connection in MethodQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Methods.sql")]
internal static partial class MethodQueries;

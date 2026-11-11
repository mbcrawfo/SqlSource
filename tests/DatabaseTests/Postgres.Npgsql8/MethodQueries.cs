namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The settings of query methods, linked from the end-to-end project, for the synchronous copies of its shapes:
// extension methods of the connection in MethodQueriesExtensions, under Npgsql 8.
[SqlSourceGenerate(Source = "../Postgres/EndToEnd/Queries/Methods.sql")]
internal static partial class MethodQueries;

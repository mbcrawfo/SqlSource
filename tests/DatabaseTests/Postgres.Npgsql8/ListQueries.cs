namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The list parameters, linked from the end-to-end project: extension methods of the connection in
// ListQueriesExtensions, each of which sends a parameter for each value of a list, under Npgsql 8.
[SqlSourceGenerate(Source = "../Postgres/EndToEnd/Queries/Lists.sql")]
internal static partial class ListQueries;

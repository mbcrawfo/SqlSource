namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The list parameters, by the project's settings file: extension methods of the connection in ListQueriesExtensions,
// each of which sends a parameter for each value of a list.
[SqlSourceGenerate(Source = "Queries/Lists.sql")]
internal static partial class ListQueries;

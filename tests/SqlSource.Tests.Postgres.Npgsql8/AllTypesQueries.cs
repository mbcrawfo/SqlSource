namespace SqlSource.Tests.Postgres.Npgsql8;

// The queries of every type of the type map, linked from the end-to-end project, by the project's output: their
// methods, extension methods of the connection in AllTypesQueriesExtensions, and their models under Npgsql 8.
[SqlSourceGenerate(
    Path = "../SqlSource.Tests.Postgres/EndToEnd/Queries/AllTypes.sql",
    SqlLocation = SqlLocation.Direct
)]
internal static partial class AllTypesQueries;

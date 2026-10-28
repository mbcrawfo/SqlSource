namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of every type of the type map, linked from the end-to-end project, by the output of that project's
// settings file, which the linked file's folder takes: their methods, extension methods of the connection in
// AllTypesQueriesExtensions, and their models under Npgsql 8.
[SqlSourceGenerate(
    Path = "../Postgres/EndToEnd/Queries/AllTypes.sql",
    SqlLocation = SqlLocation.Direct
)]
internal static partial class AllTypesQueries;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of every type of the type map, linked from the end-to-end project, by the output of that project's
// settings file, which the linked file's folder takes: their methods, extension methods of the connection in
// AllTypesQueriesExtensions, and their models under Npgsql 8.  The file's ForDapper copies put their constants on the
// type by their -- member-location: markers, for the Dapper test.
[SqlSourceGenerate(Source = "../Postgres/EndToEnd/Queries/AllTypes.sql")]
internal static partial class AllTypesQueries;

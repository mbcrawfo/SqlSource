namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// Every type of the type map, read and written through the methods, extension methods of the connection in
// AllTypesQueriesExtensions, into the project's record models.  The file's ForDapper copies, ListAllTypesForDapper and
// InsertAllTypesForDapper, put their constants on the type by their -- member-location: markers, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/AllTypes.sql")]
internal static partial class AllTypesQueries;

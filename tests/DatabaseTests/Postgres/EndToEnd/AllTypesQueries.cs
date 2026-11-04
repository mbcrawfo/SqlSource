namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// Every type of the type map, read and written through the methods, extension methods of the connection in
// AllTypesQueriesExtensions, into the project's record models.  The direct location also puts the constants of the
// file's ForDapper copies, ListAllTypesForDapper and InsertAllTypesForDapper, on the type, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AllTypesQueries;

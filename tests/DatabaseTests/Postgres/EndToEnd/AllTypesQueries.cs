namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// Every type of the type map, read and written through the methods, extension methods of the connection in
// AllTypesQueriesExtensions, into the project's record models.
[SqlSourceGenerate(Path = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AllTypesQueries;

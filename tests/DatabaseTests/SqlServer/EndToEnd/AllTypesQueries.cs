namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// Every type of the type map that SQL Server 2017 has, read and written, with a query's type override.  The file's
// ForDapper copies put their constants on the type by their -- member-location: markers, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/AllTypes.sql")]
internal static partial class AllTypesQueries;

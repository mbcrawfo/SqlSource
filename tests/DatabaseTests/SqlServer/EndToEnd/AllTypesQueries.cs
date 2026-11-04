namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// Every type of the type map that SQL Server 2017 has, read and written, with a query's type override.  The direct
// location puts the constants of the file's ForDapper copies on the type, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/AllTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class AllTypesQueries;

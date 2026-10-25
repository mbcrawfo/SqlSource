namespace SqlSource.Tests.SqlServer.EndToEnd;

// The CLR types of SQL Server, read and sent as the types of Microsoft.SqlServer.Types that the file's type override
// names: extension methods of the connection in ClrTypeQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/ClrTypes.sql")]
internal static partial class ClrTypeQueries;

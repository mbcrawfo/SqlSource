namespace SqlSource.Tests.SqlServer.EndToEnd;

// Every type of the type map that SQL Server 2017 has, read and written, with a query's type override.
[SqlSourceGenerate(Path = "Queries/AllTypes.sql")]
internal static partial class AllTypesQueries;

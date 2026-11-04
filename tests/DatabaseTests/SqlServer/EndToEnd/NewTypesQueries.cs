namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The types that SQL Server 2025 brings, json and vector, from a file described on 2025 alone.  The direct
// location puts the constant of the file's ForDapper copy on the type, for the Dapper test.
[SqlSourceGenerate(Source = "Queries/NewTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NewTypesQueries;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The types that SQL Server 2025 brings, json and vector, from a file described on 2025 alone.
[SqlSourceGenerate(Path = "Queries/NewTypes.sql")]
internal static partial class NewTypesQueries;

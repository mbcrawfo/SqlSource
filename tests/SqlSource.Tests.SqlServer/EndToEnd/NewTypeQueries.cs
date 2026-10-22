namespace SqlSource.Tests.SqlServer.EndToEnd;

// The types that SQL Server 2025 brings, json and vector, from a file described on 2025 alone.
[SqlSourceGenerate(Path = "Queries/NewTypes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NewTypeQueries;

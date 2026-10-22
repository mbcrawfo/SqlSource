namespace SqlSource.Tests.SqlServer.EndToEnd;

// INSERT, UPDATE and MERGE, each described by the rows of its OUTPUT.
[SqlSourceGenerate(Path = "Queries/Writes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class WriteQueries;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// INSERT, UPDATE and MERGE, each described by the rows of its OUTPUT.
[SqlSourceGenerate(Source = "Queries/Writes.sql")]
internal static partial class WriteQueries;

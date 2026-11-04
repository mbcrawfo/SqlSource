namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// INSERT, UPDATE and MERGE, each described by the rows of its OUTPUT.  The file's ForDapper copy puts
// its constant on the type by its -- member-location:, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/Writes.sql")]
internal static partial class WriteQueries;

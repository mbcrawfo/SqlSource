namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// INSERT, UPDATE and MERGE, each described by the rows of its OUTPUT.  The direct location puts the
// constant of the file's ForDapper copy on the type, for the Dapper tests.
[SqlSourceGenerate(Source = "Queries/Writes.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class WriteQueries;

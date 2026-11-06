namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The list parameters, linked from the end-to-end project: extension methods of the connection in
// ListQueriesExtensions, each of which sends a parameter for each value of a list, under SqlClient 6.1.
[SqlSourceGenerate(Source = "../SqlServer/EndToEnd/Queries/Lists.sql")]
internal static partial class ListQueries;

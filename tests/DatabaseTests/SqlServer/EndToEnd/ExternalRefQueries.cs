namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The project's settings, with a type override of the file header to a value type of the project, read and sent
// through its converters: extension methods of the connection in ExternalRefQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/ExternalRefs.sql")]
internal static partial class ExternalRefQueries;

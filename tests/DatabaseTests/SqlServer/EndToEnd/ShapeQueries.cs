namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The methods on the type, each taking the connection first, by MethodLocation.  The model shapes, the suffixes, the
// model namespace and the collection type, IReadOnlyList, are the file header's.
[SqlSourceGenerate(Source = "Queries/Shapes.sql", MethodLocation = MethodLocation.Public)]
internal static partial class ShapeQueries;

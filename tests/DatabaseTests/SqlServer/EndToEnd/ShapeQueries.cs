namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The methods on the type, public, each taking the connection first, the model shapes, the suffixes, the model
// namespace and the collection type, IReadOnlyList, are the file header's.
[SqlSourceGenerate(Source = "Queries/Shapes.sql")]
internal static partial class ShapeQueries;

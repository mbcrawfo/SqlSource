namespace SqlSource.DatabaseTests.QueryMethodAllocation.SqlServer;

// The claim of the SQL Server end-to-end project's ShapeQueries, repeated so that the methods measured here are the
// ones that project runs.  The methods on the type, public, each taking the connection first, the model shapes, the
// suffixes, the model namespace, that project's SqlSource.DatabaseTests.SqlServer.EndToEnd.Shapes, and the collection
// type, IReadOnlyList, are the file header's.
[SqlSourceGenerate(Source = "../../SqlServer/EndToEnd/Queries/Shapes.sql")]
internal static partial class ShapeQueries;

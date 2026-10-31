namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of spatial_values, both files linked from the end-to-end project, by the settings of their own folder,
// which turn NetTopologySuite on: their methods, extension methods of the connection in
// NetTopologySuiteQueriesExtensions, and their models in NetTopologySuite's geometries, read through the column's
// stream and written as SqlBytes by the generated NetTopologySuiteConversions on SqlClient 6.1, NetTopologySuite 2.5.0
// and NetTopologySuite.IO.SqlServerBytes 2.1.0.
[SqlSourceGenerate(Path = "../SqlServer/EndToEnd/NetTopologySuite/Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class NetTopologySuiteQueries;

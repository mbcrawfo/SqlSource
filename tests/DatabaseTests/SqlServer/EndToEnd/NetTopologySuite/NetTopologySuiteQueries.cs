namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.NetTopologySuite;

// The queries of spatial_values, both files of Queries/, whose folder's settings file turns NetTopologySuite on, so
// that their models and methods take NetTopologySuite's geometries, read through the column's stream and written as
// SqlBytes by the generated NetTopologySuiteConversions: extension methods of the connection in
// NetTopologySuiteQueriesExtensions.
[SqlSourceGenerate(Path = "Queries", SqlLocation = SqlLocation.Direct)]
internal static partial class NetTopologySuiteQueries;

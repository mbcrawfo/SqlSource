namespace SqlSource.DatabaseTests.Postgres.EndToEnd.NetTopologySuite;

// The queries of spatial_values, whose folder's settings file turns NetTopologySuite back on over the project's off
// and names the database "gis", so that their models and methods take NetTopologySuite's Geometry, read and written
// through Npgsql.NetTopologySuite: extension methods of the connection in NetTopologySuiteQueriesExtensions.
[SqlSourceGenerate(Source = "Queries/NetTopologySuite.sql")]
internal static partial class NetTopologySuiteQueries;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd.NetTopologySuite;

// The queries of spatial_values, whose folder's settings file leaves NetTopologySuite on and names the database "gis",
// so that their models and methods take NetTopologySuite's Geometry, read and written through Npgsql.NetTopologySuite:
// extension methods of the connection in NetTopologySuiteQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/NetTopologySuite.sql", SqlLocation = SqlLocation.Direct)]
internal static partial class NetTopologySuiteQueries;

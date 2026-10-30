namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of spatial_values, linked from the end-to-end project, by the settings of their own folder, which leave
// NetTopologySuite on and name the database "gis": their methods, extension methods of the connection in
// NetTopologySuiteQueriesExtensions, and their models in NetTopologySuite's Geometry, read and written through
// Npgsql.NetTopologySuite 8.
[SqlSourceGenerate(
    Path = "../Postgres/EndToEnd/NetTopologySuite/Queries/NetTopologySuite.sql",
    SqlLocation = SqlLocation.Direct
)]
internal static partial class NetTopologySuiteQueries;

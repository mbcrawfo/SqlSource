namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The type of the map that only Npgsql 10 reads, cube, written and read by its method: an extension method of the
// connection in CubeQueriesExtensions.
[SqlSourceGenerate(Path = "Queries/Cube.sql")]
internal static partial class CubeQueries;

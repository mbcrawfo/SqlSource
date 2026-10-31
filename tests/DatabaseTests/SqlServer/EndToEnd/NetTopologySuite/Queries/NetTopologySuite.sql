-- The queries of spatial_values' geography and geometry columns, claimed by NetTopologySuiteQueries here and in
-- SqlServer.SqlClient61.  The folder's settings file turns NetTopologySuite on, so every column and parameter of a
-- spatial type takes its Geometry, read through the column's stream and written as SqlBytes by the generated
-- NetTopologySuiteConversions.

-- name: InsertSpatialValues
-- summary: Inserts a row of spatial_values with a parameter for its geography and its geometry, either of which may be
-- null.
-- param: @geographyValue null
-- param: @geometryValue null
INSERT INTO dbo.spatial_values (id, geography_value, geometry_value)
VALUES (@id, @geographyValue, @geometryValue);

-- name: GetSpatialValues -> one
-- summary: The row of spatial_values with the id, its geography and its geometry.
SELECT id, geography_value, geometry_value
FROM dbo.spatial_values
WHERE id = @id;

-- name: ListGeographyValues
-- summary: The id and the geography of every row of spatial_values, with no parameter, so that a connection that wraps
-- SqlClient's can run it.
SELECT id, geography_value
FROM dbo.spatial_values
ORDER BY id;

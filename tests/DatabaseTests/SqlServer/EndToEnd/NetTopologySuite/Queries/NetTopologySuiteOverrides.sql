-- The queries of spatial_values' two columns that the file header's type overrides make NetTopologySuite's Point,
-- through geography, and its Polygon, through geometry: the read is cast to the type the override names.
-- type-override: geography = NetTopologySuite.Geometries.Point
-- type-override: geometry = NetTopologySuite.Geometries.Polygon

-- name: InsertSpatialOverrides
-- summary: Inserts a row of spatial_values with a parameter for its point and its polygon, either of which may be null.
-- param: @pointValue null
-- param: @polygonValue null
INSERT INTO dbo.spatial_values (id, point_value, polygon_value)
VALUES (@id, @pointValue, @polygonValue);

-- name: GetSpatialOverrides -> one
-- summary: The row of spatial_values with the id, its point and its polygon.
SELECT id, point_value, polygon_value
FROM dbo.spatial_values
WHERE id = @id;

-- The queries of spatial_values, claimed by NetTopologySuiteQueries here and in Postgres.Npgsql8.  The folder's
-- settings file leaves NetTopologySuite on, so every column and parameter of PostGIS takes its Geometry, read and
-- written through Npgsql.NetTopologySuite on a data source that calls UseNetTopologySuite().

-- name: InsertSpatialValues
-- summary: Inserts a row of spatial_values with a parameter for each column, any of which may be null.
-- param: @geometryValue null
-- param: @geographyValue null
-- param: @pointValue null
-- param: @polygonValue null
-- param: @geometryArrayValue null
-- param: @shapeValue null
INSERT INTO spatial_values (
    id, geometry_value, geography_value, point_value, polygon_value, geometry_array_value, shape_value
) VALUES (
    @id, @geometryValue, @geographyValue, @pointValue, @polygonValue, @geometryArrayValue, @shapeValue
);

-- name: GetSpatialValues -> one
-- summary: The row of spatial_values with the id, every column read.
SELECT *
FROM spatial_values
WHERE id = @id;

-- name: GetPointValue -> one
-- summary: The point of the row with the id, read as NetTopologySuite's Point by the query's type override.
-- type-override: public.geometry = NetTopologySuite.Geometries.Point
SELECT point_value
FROM spatial_values
WHERE id = @id;

-- name: GetPolygonValue -> one
-- summary: The polygon of the row with the id, read as NetTopologySuite's Polygon by the query's type override.
-- type-override: public.geometry = NetTopologySuite.Geometries.Polygon
SELECT polygon_value
FROM spatial_values
WHERE id = @id;

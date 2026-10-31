-- The rows of the database "gis".  PostgisDatabases and tools/describe-end-to-end.sh apply this after schema.sql, and a
-- change to it is followed by a run of tools/describe-end-to-end.sh.
--
-- spatial_values: row 1 has a value in every column, written by the server from its text, with a point of four
-- dimensions, Z and M, in geometry_value, and a NULL element in geometry_array_value.

INSERT INTO spatial_values (
    id, geometry_value, geography_value, point_value, polygon_value, geometry_array_value, shape_value
) VALUES (
    1,
    ST_GeomFromText('POINT ZM(1 2 3 4)', 3857),
    ST_GeogFromText('SRID=4326;POINT(-71.06 42.36)'),
    ST_GeomFromText('POINT(10 20)', 4326),
    ST_GeomFromText('POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))', 4326),
    ARRAY[ST_GeomFromText('POINT(1 1)', 4326), NULL, ST_GeomFromText('LINESTRING(0 0, 1 1)', 3857)],
    ST_GeomFromText('LINESTRING(0 0, 2 2)', 4326)
);

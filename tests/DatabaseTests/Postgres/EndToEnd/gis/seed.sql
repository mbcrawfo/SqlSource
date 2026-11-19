-- The rows of the database "gis".  PostgisDatabases and tools/describe-end-to-end.sh apply this after schema.sql, and a
-- change to it is followed by a run of tools/describe-end-to-end.sh.
--
-- spatial_values: row 1 has a value in every column, written by the server from its text, with a point of four
-- dimensions, Z and M, in geometry_value, and a NULL element in geometry_array_value.

INSERT INTO spatial_values (
    id, geometry_value, geography_value, point_value, polygon_value, geometry_array_value, shape_value
) VALUES (
    1,
    ST_GEOMFROMTEXT('POINT ZM(1 2 3 4)', 3857),
    ST_GEOGFROMTEXT('SRID=4326;POINT(-71.06 42.36)'),
    ST_GEOMFROMTEXT('POINT(10 20)', 4326),
    ST_GEOMFROMTEXT('POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))', 4326),
    ARRAY[ST_GEOMFROMTEXT('POINT(1 1)', 4326), NULL, ST_GEOMFROMTEXT('LINESTRING(0 0, 1 1)', 3857)],
    ST_GEOMFROMTEXT('LINESTRING(0 0, 2 2)', 4326)
);

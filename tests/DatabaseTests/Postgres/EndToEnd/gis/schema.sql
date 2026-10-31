-- The schema of the database "gis", which the queries of NetTopologySuite/ are described against and run on, on the
-- PostGIS image alone.  PostgisDatabases and tools/describe-end-to-end.sh apply it, then seed.sql.  A change to it is
-- followed by a run of tools/describe-end-to-end.sh, which writes the sidecars again.

CREATE EXTENSION postgis;

-- A domain over geometry, which the library reads as its base.
CREATE DOMAIN shape AS geometry;

-- A column of each of PostGIS's types: geometry and geography with no modifier, geometry with a modifier of each of
-- the two shapes that NetTopologySuite/Queries/NetTopologySuite.sql reads by a type override, an array and a domain.
CREATE TABLE spatial_values (
    id integer PRIMARY KEY,
    geometry_value geometry,
    geography_value geography,
    point_value geometry(Point, 4326),
    polygon_value geometry(Polygon, 4326),
    geometry_array_value geometry[],
    shape_value shape
);

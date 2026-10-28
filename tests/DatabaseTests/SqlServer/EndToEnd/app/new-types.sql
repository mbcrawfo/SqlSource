-- The table of the types that SQL Server 2025 brings, json and vector, in the database "app".  SqlServerContainer and
-- tools/describe-end-to-end.sh apply it after seed.sql, on a server of major 17 or later alone, since 2017 has neither
-- type.  A change to it is followed by a run of tools/describe-end-to-end.sh.
--
-- Row 1 has a value in both columns, the vector's elements binary fractions, exact as a float, and row 2 NULL in both.

CREATE TABLE dbo.new_types (
    id int PRIMARY KEY,
    json_value json NULL,
    vector_value vector(3) NULL
);

INSERT INTO dbo.new_types (id, json_value, vector_value) VALUES
    (1, N'{"name":"Zoë","tags":["a","b"]}', '[1.5,-2.25,0.5]'),
    (2, NULL, NULL);

-- The queries of Sql/: their output is sql, set by the marker below, so each is only its SQL, a constant or a method,
-- and needs no description, no sidecar and no connection.  The second marker names the class that holds them, which
-- is Sql by default.
-- output: sql
-- nested-class: Counts

-- name: CountUsers
-- summary: The number of users.
SELECT COUNT(*) FROM users;

-- name: CountRows
-- summary: The number of rows of a table that the code chooses.
SELECT COUNT(*) FROM {{table}};

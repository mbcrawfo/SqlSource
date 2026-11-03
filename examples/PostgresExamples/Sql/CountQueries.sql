-- The queries of Sql/: their output is sql, set by the attribute of CountQueries, so each is only its SQL, a constant
-- or a method, and needs no description, no sidecar and no connection.

-- name: CountUsers
-- summary: The number of users.
SELECT count(*) FROM users;

-- name: CountRows
-- summary: The number of rows of a table that the code chooses.
SELECT count(*) FROM {{table}};

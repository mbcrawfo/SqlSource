-- The queries of the users, with the output models of this folder's sqlsource.json.

-- name: GetUser
-- summary: One user, every column of the table.
-- Every column of the table, which its table model takes as it is.
SELECT *  -- noqa: AM04
FROM users
WHERE id = @userId;

-- The queries of the users, with the output models of this folder's sqlsource.json.

-- name: GetUser
-- summary: One user, every column of the table.
SELECT *
FROM users
WHERE id = @userId;

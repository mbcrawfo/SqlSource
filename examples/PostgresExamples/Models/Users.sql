-- The queries of the users, with the output models of StoreQueries.

-- name: GetUser
-- summary: One user, every column of the table.
SELECT *
FROM users
WHERE id = @userId;

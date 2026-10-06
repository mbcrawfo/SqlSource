-- Queries for the users table.

-- name: GetUser
-- summary: Loads one user by id.
SELECT id, name -- the columns the model needs
FROM users
WHERE id = @id;

-- name: ListUsers
SELECT id, name
FROM users
ORDER BY name;

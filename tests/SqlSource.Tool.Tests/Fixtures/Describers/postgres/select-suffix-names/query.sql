-- name: GetUserFlags
SELECT deleted_at AS "deleted_at!", name AS "name?" FROM users WHERE id = @id;

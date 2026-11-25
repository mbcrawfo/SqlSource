-- dialect: mysql
-- name: A
SELECT {{t:users}} -- c, {{u:x}}
FROM t  WHERE id = @id
-- name: B
SELECT 1

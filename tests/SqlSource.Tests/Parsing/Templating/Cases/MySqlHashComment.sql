-- dialect: mysql
-- name: Hashed
SELECT * FROM users # TODO {{f:
WHERE {{cond:active = 1}} AND id = @id;

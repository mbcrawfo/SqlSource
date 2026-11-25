-- dialect: mysql
-- name: A
-- variable: @n
SET @n := 1;
SELECT {{t:users}} WHERE id IN @ids AND x = @n AND y IN @n

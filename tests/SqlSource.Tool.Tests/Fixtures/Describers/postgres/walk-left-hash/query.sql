-- name: WalkLeftByName
SELECT u.id, u.name, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.name = @name;

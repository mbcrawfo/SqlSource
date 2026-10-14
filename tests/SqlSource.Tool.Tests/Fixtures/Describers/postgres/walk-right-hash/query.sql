-- name: WalkRight
SELECT u.id, u.name, o.id AS order_id, o.total
FROM orders o RIGHT JOIN users u ON o.user_id = u.id
WHERE u.name = @name;

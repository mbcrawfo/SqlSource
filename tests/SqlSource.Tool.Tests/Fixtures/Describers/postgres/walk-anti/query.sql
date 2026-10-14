-- name: WalkAnti
SELECT u.id, o.id AS order_id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE o.user_id IS NULL;

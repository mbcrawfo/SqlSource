-- name: GetUserOrders
SELECT u.id, u.email, o.total, count(*) OVER () AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE u.id = @id;

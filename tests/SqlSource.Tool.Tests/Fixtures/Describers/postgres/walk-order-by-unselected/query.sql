-- name: WalkOrderByUnselected
SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
ORDER BY o.placed_at;

-- name: WalkProvedEmpty
SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id AND 1 = 0;

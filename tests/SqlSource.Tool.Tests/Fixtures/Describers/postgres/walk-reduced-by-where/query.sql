-- name: WalkReducedByWhere
SELECT u.id, o.total
FROM users u LEFT JOIN orders o ON o.user_id = u.id
WHERE o.total > @min;

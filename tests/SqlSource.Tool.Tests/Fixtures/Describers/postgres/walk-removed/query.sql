-- name: WalkRemoved
SELECT o.id, o.total
FROM orders o LEFT JOIN users u ON u.id = o.user_id
WHERE o.total > @min;

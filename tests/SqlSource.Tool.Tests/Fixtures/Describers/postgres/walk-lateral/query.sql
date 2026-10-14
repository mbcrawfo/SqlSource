-- name: WalkLateral
SELECT u.id, l.total
FROM users u
LEFT JOIN LATERAL (SELECT o.total FROM orders o WHERE o.user_id = u.id) l ON true;

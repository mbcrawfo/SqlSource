-- name: WalkUnion
SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id
UNION ALL
SELECT u.id, o.total FROM users u RIGHT JOIN orders o ON o.user_id = u.id;

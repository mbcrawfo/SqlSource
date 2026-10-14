-- name: WalkCteMaterialized
WITH s AS MATERIALIZED (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id)
SELECT s.id, s.total FROM s;

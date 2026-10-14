-- name: WalkViewNullableSide
SELECT o.id, a.name
FROM orders o LEFT JOIN active_users a ON a.id = o.user_id;

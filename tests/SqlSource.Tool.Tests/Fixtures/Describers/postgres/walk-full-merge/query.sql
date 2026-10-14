-- name: WalkFull
SELECT u.id, o.total
FROM users u FULL JOIN orders o ON o.user_id = u.id;

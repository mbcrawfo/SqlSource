-- name: WalkCteInlined
WITH s AS (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id)
SELECT s.id, s.total FROM s WHERE s.id = @id;

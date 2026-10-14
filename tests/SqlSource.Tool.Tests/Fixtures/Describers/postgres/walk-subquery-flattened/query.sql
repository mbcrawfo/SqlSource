-- name: WalkSubqueryFlattened
SELECT s.id, s.total
FROM (SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id) s
WHERE s.id = @id;

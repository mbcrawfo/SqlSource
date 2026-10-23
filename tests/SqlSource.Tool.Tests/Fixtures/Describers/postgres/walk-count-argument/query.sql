-- name: WalkCountArgument
SELECT u.id, count(o.id) AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
GROUP BY u.id;

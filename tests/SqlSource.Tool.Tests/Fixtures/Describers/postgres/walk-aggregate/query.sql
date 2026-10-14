-- name: WalkAggregate
SELECT u.id, o.user_id, count(*) AS n
FROM users u LEFT JOIN orders o ON o.user_id = u.id
GROUP BY u.id, o.user_id;

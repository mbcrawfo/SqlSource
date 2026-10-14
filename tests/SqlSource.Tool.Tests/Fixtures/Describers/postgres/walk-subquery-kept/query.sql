-- name: WalkSubqueryKept
SELECT s.id, s.total, s.n
FROM (
    SELECT u.id, o.total, row_number() OVER (ORDER BY u.id) AS n
    FROM users u LEFT JOIN orders o ON o.user_id = u.id
) s
WHERE s.n > @n;

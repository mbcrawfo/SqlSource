-- name: ListAccountsThatOrderedAndLoggedIn
SELECT a.id, a.name, a.email
FROM accounts a
JOIN orders o ON o.account_id = a.id
WHERE o.placed_at >= @since
INTERSECT
SELECT a.id, a.name, a.email
FROM accounts a
JOIN events e ON e.account_id = a.id
WHERE e.kind = 'login' AND e.at >= @since
ORDER BY name;

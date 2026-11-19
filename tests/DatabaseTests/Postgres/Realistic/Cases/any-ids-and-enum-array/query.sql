-- name: ListAccountsByIdsAndStatuses
SELECT
    a.id,
    a.name,
    a.status,
    a.email,
    count(o.id) AS order_count
FROM accounts a
LEFT JOIN orders o ON o.account_id = a.id
WHERE a.id = any(@ids) AND a.status = any(@statuses)
GROUP BY a.id
ORDER BY a.name;

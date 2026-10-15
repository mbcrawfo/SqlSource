-- name: ListAccountActivitySince
WITH activity AS (
    SELECT o.account_id, o.placed_at AS at, 'order' AS kind, o.id AS ref_id
    FROM orders o
    UNION ALL
    SELECT e.account_id, e.at, e.kind, e.id
    FROM events e
)
SELECT a.id, a.name, a.email, act.at, act.kind, act.ref_id
FROM accounts a
LEFT JOIN activity act ON act.account_id = a.id AND act.at >= @since
WHERE a.closed_at IS NULL
ORDER BY a.id, act.at DESC;

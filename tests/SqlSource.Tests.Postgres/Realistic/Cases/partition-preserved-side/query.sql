-- name: ListEventsSince
SELECT e.id, e.at, e.kind, e.payload ->> 'device' AS device, a.name AS account_name, a.closed_at
FROM events e
LEFT JOIN accounts a ON a.id = e.account_id
WHERE e.at >= @from
ORDER BY e.at;

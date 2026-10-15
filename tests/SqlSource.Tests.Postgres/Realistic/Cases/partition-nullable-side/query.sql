-- name: ListOpenAccountsWithRecentEvents
SELECT a.id, a.name, e.kind, e.at, e.payload ->> 'ip' AS ip
FROM accounts a
LEFT JOIN events e ON e.account_id = a.id AND e.at >= @from
WHERE a.status <> 'closed'
ORDER BY a.id, e.at;

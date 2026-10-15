-- name: ListAccountsAndTheirOrderIds
SELECT a.id, o.id, a.name, o.placed_at
FROM accounts a
LEFT JOIN orders o ON o.account_id = a.id
WHERE a.status = @status
ORDER BY a.id, o.id;

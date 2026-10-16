-- name: ListAccountsWithOrderRows
SELECT a.id, a.name, array_agg(ROW(o.id, o.placed_at, o.note) ORDER BY o.placed_at) AS orders
FROM accounts a
JOIN orders o ON o.account_id = a.id
GROUP BY a.id
ORDER BY a.id;

-- name: ListOrdersWithOpenAccount
SELECT coalesce(a.id, o.account_id) AS account_id, a.name, a.status, o.id AS order_id, o.placed_at
FROM accounts a
RIGHT JOIN orders o ON o.account_id = a.id AND a.status <> 'closed'
ORDER BY o.placed_at;

-- name: WalkChain
SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
LEFT JOIN billing.invoices i ON i.order_id = o.id;

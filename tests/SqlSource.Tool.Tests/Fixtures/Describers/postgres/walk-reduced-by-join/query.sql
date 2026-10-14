-- name: WalkReducedByJoin
SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN orders o ON o.user_id = u.id
JOIN billing.invoices i ON i.order_id = o.id;

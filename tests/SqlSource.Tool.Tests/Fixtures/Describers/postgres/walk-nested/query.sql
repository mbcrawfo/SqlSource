-- name: WalkNested
SELECT u.id, o.total, i.amount
FROM users u
LEFT JOIN (orders o JOIN billing.invoices i ON i.order_id = o.id) ON o.user_id = u.id;

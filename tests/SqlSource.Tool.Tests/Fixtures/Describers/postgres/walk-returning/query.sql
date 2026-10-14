-- name: WalkReturning
UPDATE users u SET name = @name
FROM orders o LEFT JOIN billing.invoices i ON i.order_id = o.id
WHERE o.user_id = u.id
RETURNING u.id, o.total, i.amount;

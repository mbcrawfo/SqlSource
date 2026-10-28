-- name: ListLatestOrderPerAccount
SELECT DISTINCT ON (o.account_id)
    o.account_id,
    a.name,
    o.id AS order_id,
    o.placed_at,
    o.shipped_at,
    i.amount,
    i.paid_on
FROM orders o
JOIN accounts a ON a.id = o.account_id
LEFT JOIN billing.invoices i ON i.order_id = o.id
ORDER BY o.account_id, o.placed_at DESC, i.issued_on DESC;

-- name: ListAccountsWithLatestOrderAndInvoice
SELECT a.id, a.name, lo.order_id, lo.placed_at, lo.shipped_at, lo.amount
FROM accounts a
LEFT JOIN LATERAL (
    SELECT o.id AS order_id, o.placed_at, o.shipped_at, i.amount
    FROM orders o
    LEFT JOIN billing.invoices i ON i.order_id = o.id
    WHERE o.account_id = a.id
    ORDER BY o.placed_at DESC
    LIMIT 1
) lo ON true
ORDER BY a.name;

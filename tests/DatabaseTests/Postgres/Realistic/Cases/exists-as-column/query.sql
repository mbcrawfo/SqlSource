-- name: ListAccountFlags
SELECT
    a.id,
    a.name,
    a.email,
    EXISTS (SELECT 1 FROM orders o WHERE o.account_id = a.id) AS has_orders,
    EXISTS (
        SELECT 1
        FROM orders o
        LEFT JOIN billing.invoices i ON i.order_id = o.id
        WHERE o.account_id = a.id AND i.id IS NULL
    ) AS has_uninvoiced_orders,
    a.closed_at IS NOT NULL AS is_closed
FROM accounts a
ORDER BY a.name
LIMIT 10;

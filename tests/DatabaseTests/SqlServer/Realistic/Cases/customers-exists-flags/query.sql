-- name: ListCustomersInGoodStanding
-- param: @as_of date
SELECT
    c.id,
    c.name,
    c.email,
    CASE WHEN EXISTS (SELECT 1 FROM dbo.orders AS o WHERE o.customer_id = c.id) THEN 1 ELSE 0 END AS has_orders,
    CAST(
        CASE
            WHEN EXISTS (
                SELECT 1
                FROM dbo.carts AS ca
                WHERE ca.customer_id = c.id
                    AND ca.checked_out_at IS NULL
            ) THEN 1
            ELSE 0
        END AS bit
    ) AS has_open_cart
FROM dbo.customers AS c
WHERE c.status <> 'closed'
    AND NOT EXISTS (
        SELECT 1
        FROM billing.invoices AS i
        JOIN dbo.orders AS o ON o.id = i.order_id
        WHERE o.customer_id = c.id
            AND i.voided_at IS NULL
            AND i.due_at < @as_of
            AND NOT EXISTS (SELECT 1 FROM billing.payments AS p WHERE p.invoice_id = i.id)
    )
ORDER BY c.name;

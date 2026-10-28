-- name: ListLatestOrderPerCustomer
WITH ranked AS (
    SELECT
        o.id,
        o.customer_id,
        o.placed_at,
        o.status,
        o.ship_city,
        o.shipped_at,
        ROW_NUMBER() OVER (PARTITION BY o.customer_id ORDER BY o.placed_at DESC, o.id DESC) AS rn
    FROM dbo.orders AS o
    WHERE o.status <> 'cancelled'
)
SELECT
    r.customer_id,
    c.name AS customer_name,
    r.id AS order_id,
    r.placed_at,
    r.status,
    r.ship_city,
    r.shipped_at,
    i.number AS invoice_number
FROM ranked AS r
JOIN dbo.customers AS c ON c.id = r.customer_id
LEFT JOIN billing.invoices AS i ON i.order_id = r.id
WHERE r.rn = 1
ORDER BY r.placed_at DESC, r.customer_id;

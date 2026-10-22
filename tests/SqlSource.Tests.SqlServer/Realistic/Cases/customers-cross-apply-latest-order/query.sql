-- name: ListCustomersWithLatestOrder
SELECT
    c.id,
    c.name,
    c.email,
    lo.id AS order_id,
    lo.placed_at,
    lo.status,
    lo.ship_city,
    lo.shipped_at
FROM dbo.customers AS c
CROSS APPLY (
    SELECT TOP (1) o.id, o.placed_at, o.status, o.ship_city, o.shipped_at
    FROM dbo.orders AS o
    WHERE o.customer_id = c.id
    ORDER BY o.placed_at DESC, o.id DESC
) AS lo
ORDER BY lo.placed_at DESC, c.id;

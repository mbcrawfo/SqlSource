-- name: MarkShippedOrdersDelivered
-- param: @shipped_before datetime2(0)
UPDATE o
SET o.status = 'delivered'
OUTPUT
    inserted.id,
    deleted.status AS previous_status,
    inserted.status,
    inserted.shipped_at,
    inserted.ship_city,
    c.name AS customer_name,
    c.email AS customer_email
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.status = 'shipped'
    AND o.shipped_at < @shipped_before;

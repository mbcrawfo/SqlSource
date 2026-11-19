-- name: MarkShippedOrdersDelivered
-- param: @shipped_before datetime2(0)
UPDATE o
SET o.status = 'delivered'
OUTPUT
    INSERTED.id,
    DELETED.status AS previous_status,
    INSERTED.status,
    INSERTED.shipped_at,
    INSERTED.ship_city,
    c.name AS customer_name,
    c.email AS customer_email
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE
    o.status = 'shipped'
    AND o.shipped_at < @shipped_before;

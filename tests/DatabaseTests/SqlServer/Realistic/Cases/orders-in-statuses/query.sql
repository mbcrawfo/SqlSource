-- name: ListOrdersInStatuses
-- param: @status_1 varchar(10)
-- param: @status_2 varchar(10)
SELECT
    o.id,
    o.customer_id,
    o.placed_at,
    o.status,
    o.ship_name,
    o.ship_city,
    o.shipped_at
FROM dbo.orders AS o
WHERE o.status IN (@status_1, @status_2)
    AND o.customer_id IN (
        SELECT c.id
        FROM dbo.customers AS c
        WHERE c.status IN ('active', 'suspended')
    )
ORDER BY o.placed_at;

-- name: SearchOrders
-- param: @customer_id int null
-- param: @status varchar(10) null
-- param: @placed_from datetime2(0) null
-- param: @placed_to datetime2(0) null
-- param: @ship_city nvarchar(50) null
SELECT
    o.id,
    o.placed_at,
    o.status,
    c.name AS customer_name,
    c.email AS customer_email,
    o.ship_city,
    o.shipped_at,
    o.note
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE (@customer_id IS NULL OR o.customer_id = @customer_id)
    AND (@status IS NULL OR o.status = @status)
    AND (@placed_from IS NULL OR o.placed_at >= @placed_from)
    AND (@placed_to IS NULL OR o.placed_at < @placed_to)
    AND (@ship_city IS NULL OR o.ship_city = @ship_city)
ORDER BY o.placed_at DESC, o.id DESC
OPTION (RECOMPILE);

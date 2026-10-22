-- name: ListOrdersInCustomerTimeZone
-- param: @since datetime2(0)
SELECT
    o.id,
    c.name AS customer_name,
    c.time_zone,
    o.placed_at AT TIME ZONE 'UTC' AS placed_utc,
    o.placed_at AT TIME ZONE 'UTC' AT TIME ZONE c.time_zone AS placed_local,
    o.shipped_at AT TIME ZONE 'UTC' AT TIME ZONE COALESCE(c.time_zone, 'UTC') AS shipped_local
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.placed_at >= @since
ORDER BY o.placed_at, o.id;

-- name: CountOrdersByStatusAndCity
SELECT
    CASE GROUPING(o.status) WHEN 0 THEN 'status' ELSE 'city' END AS dimension,
    o.status,
    o.ship_city,
    COUNT(*) AS order_count,
    COUNT(o.shipped_at) AS shipped_count,
    MAX(o.placed_at) AS last_placed_at
FROM dbo.orders AS o
GROUP BY GROUPING SETS ((o.status), (o.ship_city))
ORDER BY dimension DESC, o.status, o.ship_city;

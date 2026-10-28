-- name: ListCustomersFirstOrderValue
WITH order_totals AS (
    SELECT o.id, o.customer_id, o.placed_at, o.ship_city, SUM(ol.quantity * ol.unit_price) AS total
    FROM dbo.orders AS o
    JOIN dbo.order_lines AS ol ON ol.order_id = o.id
    WHERE o.status <> 'cancelled'
    GROUP BY o.id, o.customer_id, o.placed_at, o.ship_city
)
SELECT DISTINCT
    c.id,
    c.name,
    COUNT(ot.id) OVER (PARTITION BY c.id) AS order_count,
    FIRST_VALUE(ot.id) OVER (PARTITION BY c.id ORDER BY ot.placed_at, ot.id) AS first_order_id,
    FIRST_VALUE(ot.placed_at) OVER (PARTITION BY c.id ORDER BY ot.placed_at, ot.id) AS first_ordered_at,
    FIRST_VALUE(ot.total) OVER (PARTITION BY c.id ORDER BY ot.placed_at, ot.id) AS first_order_value,
    FIRST_VALUE(ot.ship_city) OVER (PARTITION BY c.id ORDER BY ot.placed_at, ot.id) AS first_ship_city
FROM dbo.customers AS c
LEFT JOIN order_totals AS ot ON ot.customer_id = c.id
ORDER BY first_ordered_at, c.id;

-- name: ListBigCustomers
-- param: @min_total decimal(12,2)
SELECT
    c.id,
    c.name,
    c.company,
    COUNT(DISTINCT o.id) AS order_count,
    SUM(ol.quantity * ol.unit_price) AS total_spent,
    MAX(o.placed_at) AS last_ordered_at
FROM dbo.customers AS c
JOIN dbo.orders AS o ON o.customer_id = c.id
JOIN dbo.order_lines AS ol ON ol.order_id = o.id
WHERE o.status <> 'cancelled'
GROUP BY c.id, c.name, c.company
HAVING SUM(ol.quantity * ol.unit_price) > @min_total
ORDER BY total_spent DESC;

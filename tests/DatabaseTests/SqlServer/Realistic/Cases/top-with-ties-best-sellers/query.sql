-- name: ListBestSellers
-- param: @top int
-- param: @since datetime2(0)
SELECT TOP (@top) WITH TIES
    p.id,
    p.sku,
    p.name,
    c.name AS category_name,
    SUM(ol.quantity) AS units_sold,
    SUM(ol.quantity * ol.unit_price) AS revenue
FROM dbo.order_lines AS ol
JOIN dbo.orders AS o ON o.id = ol.order_id
JOIN dbo.products AS p ON p.id = ol.product_id
LEFT JOIN dbo.categories AS c ON c.id = p.category_id
WHERE o.placed_at >= @since
    AND o.status <> 'cancelled'
GROUP BY p.id, p.sku, p.name, c.name
ORDER BY units_sold DESC;

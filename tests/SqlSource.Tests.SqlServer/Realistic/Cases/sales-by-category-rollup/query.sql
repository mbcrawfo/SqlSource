-- name: GetSalesByCategory
SELECT
    c.name AS category,
    p.name AS product,
    SUM(ol.quantity) AS units,
    SUM(ol.quantity * ol.unit_price) AS revenue,
    GROUPING(c.name) AS is_grand_total,
    GROUPING(p.name) AS is_category_total
FROM dbo.order_lines AS ol
JOIN dbo.orders AS o ON o.id = ol.order_id
JOIN dbo.products AS p ON p.id = ol.product_id
LEFT JOIN dbo.categories AS c ON c.id = p.category_id
WHERE o.status <> 'cancelled'
GROUP BY ROLLUP (c.name, p.name)
ORDER BY GROUPING(c.name), c.name, GROUPING(p.name), p.name;

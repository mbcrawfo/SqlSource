-- name: ListOrdersWithSkus
-- param: @customer_id int
SELECT
    o.id,
    o.placed_at,
    o.status,
    COUNT(ol.line_no) AS line_count,
    STRING_AGG(p.sku, ', ') WITHIN GROUP (ORDER BY ol.line_no) AS skus
FROM dbo.orders AS o
LEFT JOIN dbo.order_lines AS ol ON ol.order_id = o.id
LEFT JOIN dbo.products AS p ON p.id = ol.product_id
WHERE o.customer_id = @customer_id
GROUP BY o.id, o.placed_at, o.status
ORDER BY o.placed_at DESC, o.id DESC;

-- name: ListProductsNotOrderedSince
-- param: @since datetime2(0)
SELECT p.id, p.sku, p.name, p.category_id, p.discontinued_at
FROM dbo.products AS p
EXCEPT
SELECT p.id, p.sku, p.name, p.category_id, p.discontinued_at
FROM dbo.products AS p
JOIN dbo.order_lines AS ol ON ol.product_id = p.id
JOIN dbo.orders AS o ON o.id = ol.order_id
WHERE o.placed_at >= @since
    AND o.status <> 'cancelled'
ORDER BY sku;

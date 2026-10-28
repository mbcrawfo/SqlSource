-- name: ListOrdersWithLines
-- param: @customer_id int
SELECT
    o.id,
    o.placed_at,
    o.status,
    o.ship_city,
    (
        SELECT ol.line_no, p.sku, ol.quantity, ol.unit_price
        FROM dbo.order_lines AS ol
        JOIN dbo.products AS p ON p.id = ol.product_id
        WHERE ol.order_id = o.id
        ORDER BY ol.line_no
        FOR JSON PATH
    ) AS lines
FROM dbo.orders AS o
WHERE o.customer_id = @customer_id
ORDER BY o.placed_at DESC, o.id DESC;

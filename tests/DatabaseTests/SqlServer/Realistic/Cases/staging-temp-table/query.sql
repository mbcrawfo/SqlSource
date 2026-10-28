-- name: ListRestockCandidates
-- param: @since datetime2(0)
CREATE TABLE #sold (
    product_id int NOT NULL PRIMARY KEY,
    units int NOT NULL
);

INSERT INTO #sold (product_id, units)
SELECT ol.product_id, SUM(ol.quantity)
FROM dbo.order_lines AS ol
JOIN dbo.orders AS o ON o.id = ol.order_id
WHERE o.placed_at >= @since
    AND o.status <> 'cancelled'
GROUP BY ol.product_id;

SELECT p.id, p.sku, p.name, s.on_hand, s.reorder_at, so.units AS units_sold
FROM #sold AS so
JOIN dbo.products AS p ON p.id = so.product_id
LEFT JOIN dbo.stock AS s ON s.product_id = p.id
ORDER BY so.units DESC, p.sku;

DROP TABLE #sold;

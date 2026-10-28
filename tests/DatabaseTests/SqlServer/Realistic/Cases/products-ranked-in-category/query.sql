-- name: RankProductsBySales
WITH sales AS (
    SELECT ol.product_id, SUM(ol.quantity) AS units_sold, MAX(o.placed_at) AS last_sold_at
    FROM dbo.order_lines AS ol
    JOIN dbo.orders AS o ON o.id = ol.order_id
    WHERE o.status <> 'cancelled'
    GROUP BY ol.product_id
)
SELECT
    c.name AS category_name,
    p.id,
    p.sku,
    p.name,
    ISNULL(s.units_sold, 0) AS units_sold,
    s.last_sold_at,
    RANK() OVER (PARTITION BY p.category_id ORDER BY ISNULL(s.units_sold, 0) DESC) AS rank_in_category,
    DENSE_RANK() OVER (ORDER BY ISNULL(s.units_sold, 0) DESC) AS sales_tier
FROM dbo.products AS p
LEFT JOIN dbo.categories AS c ON c.id = p.category_id
LEFT JOIN sales AS s ON s.product_id = p.id
ORDER BY category_name, rank_in_category, p.name;

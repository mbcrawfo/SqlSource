-- name: ListCurrentProductSales
WITH current_products AS NOT MATERIALIZED (
    SELECT p.id, p.sku, p.name, p.price, p.category_id
    FROM products p
    WHERE p.discontinued_on IS NULL
)
SELECT cp.sku, cp.name, cp.price, c.name AS category, sum(l.quantity) AS units_sold
FROM current_products cp
LEFT JOIN categories c ON c.id = cp.category_id
LEFT JOIN order_lines l ON l.product_id = cp.id
GROUP BY cp.id, cp.sku, cp.name, cp.price, c.name
ORDER BY units_sold DESC NULLS LAST, cp.sku;

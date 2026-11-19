-- name: ListCatalog
SELECT
    c.id AS category_id,
    c.name AS category_name,
    p.id AS product_id,
    p.sku,
    p.name AS product_name,
    p.list_price,
    p.sale_price
FROM dbo.categories AS c
-- The RIGHT JOIN, whose left side can be null, is what this case describes.
RIGHT JOIN dbo.products AS p ON p.category_id = c.id  -- noqa: CV08
WHERE p.discontinued_at IS NULL
ORDER BY c.name, p.name;

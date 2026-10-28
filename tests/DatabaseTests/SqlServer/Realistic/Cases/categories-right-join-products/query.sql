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
RIGHT JOIN dbo.products AS p ON p.category_id = c.id
WHERE p.discontinued_at IS NULL
ORDER BY c.name, p.name;

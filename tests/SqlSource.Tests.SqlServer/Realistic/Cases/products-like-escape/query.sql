-- name: SearchProducts
-- param: @pattern nvarchar(100)
SELECT
    p.id,
    p.sku,
    p.name,
    p.category_id,
    p.list_price,
    p.discount_pct,
    p.discontinued_at
FROM dbo.products AS p
WHERE p.name LIKE @pattern ESCAPE '\'
    OR p.sku LIKE @pattern ESCAPE '\'
ORDER BY p.name;

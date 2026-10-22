-- name: ListProductAttributes
SELECT
    p.id,
    p.sku,
    p.name,
    a.color,
    a.weight_kg,
    a.ports,
    a.sizes
FROM dbo.products AS p
CROSS APPLY OPENJSON(p.attributes) WITH (
    color nvarchar(30) '$.color',
    weight_kg decimal(4, 2) '$.weight_kg',
    ports int '$.ports',
    sizes nvarchar(max) '$.sizes' AS JSON
) AS a
WHERE p.discontinued_at IS NULL
ORDER BY p.sku;

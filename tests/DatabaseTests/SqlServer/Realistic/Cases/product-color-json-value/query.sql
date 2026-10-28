-- name: ListProductsByColor
-- param: @color nvarchar(30) null
SELECT
    p.id,
    p.sku,
    p.name,
    JSON_VALUE(p.attributes, '$.color') AS color,
    CAST(JSON_VALUE(p.attributes, '$.weight_kg') AS decimal(4, 2)) AS weight_kg
FROM dbo.products AS p
WHERE p.discontinued_at IS NULL
    AND (@color IS NULL OR JSON_VALUE(p.attributes, '$.color') = @color)
ORDER BY p.name;

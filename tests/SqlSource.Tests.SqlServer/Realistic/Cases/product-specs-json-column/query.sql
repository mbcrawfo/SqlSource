-- name: ListProductSpecs
-- param: @category_id int null
SELECT
    p.id,
    p.sku,
    p.name,
    s.spec,
    JSON_VALUE(s.spec, '$.cpu') AS cpu,
    CAST(JSON_VALUE(s.spec, '$.ram_gb') AS int) AS ram_gb,
    JSON_QUERY(s.spec, '$.ports') AS ports
FROM dbo.products AS p
LEFT JOIN dbo.product_specs AS s ON s.product_id = p.id
WHERE @category_id IS NULL OR p.category_id = @category_id
ORDER BY p.id;

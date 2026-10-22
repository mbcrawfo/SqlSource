-- name: ListPriceHistory
-- param: @product_id int null
SELECT
    pr.product_id,
    p.sku,
    pr.price,
    pr.valid_from,
    NULLIF(pr.valid_to, CAST('9999-12-31 23:59:59.9999999' AS datetime2)) AS replaced_at
FROM dbo.prices FOR SYSTEM_TIME ALL AS pr
JOIN dbo.products AS p ON p.id = pr.product_id
WHERE @product_id IS NULL OR pr.product_id = @product_id
ORDER BY pr.product_id, pr.valid_from;

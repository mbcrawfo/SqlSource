-- name: GetPriceListAsOf
-- param: @at datetime2
SELECT
    p.id,
    p.sku,
    p.name,
    p.list_price,
    pr.price,
    pr.valid_from AS price_since,
    COALESCE(pr.price, p.list_price) AS effective_price
FROM dbo.products AS p
LEFT JOIN dbo.prices FOR SYSTEM_TIME AS OF @at AS pr ON pr.product_id = p.id
ORDER BY p.sku;

-- name: ListPriceBounds
-- param: @campaign_pct decimal(5, 2) null
SELECT
    p.id,
    p.sku,
    p.list_price,
    pr.price AS current_price,
    h.lowest_previous,
    h.highest_previous,
    LEAST(p.list_price, pr.price, h.lowest_previous) AS lowest_price,
    GREATEST(p.list_price, pr.price, h.highest_previous) AS highest_price,
    GREATEST(p.discount_pct, @campaign_pct) AS best_discount_pct
FROM dbo.products AS p
JOIN dbo.prices AS pr ON pr.product_id = p.id
OUTER APPLY (
    SELECT MIN(ph.price) AS lowest_previous, MAX(ph.price) AS highest_previous
    FROM dbo.prices_history AS ph
    WHERE ph.product_id = p.id
) AS h
ORDER BY p.sku;

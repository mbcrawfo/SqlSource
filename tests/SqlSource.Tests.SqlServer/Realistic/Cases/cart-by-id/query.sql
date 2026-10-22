-- name: GetCart
-- param: @cart_id uniqueidentifier
SELECT
    c.id AS cart_id,
    c.customer_id,
    c.created_at,
    c.checked_out_at,
    ci.product_id,
    p.sku,
    p.name AS product_name,
    ci.quantity,
    ci.added_at
FROM dbo.carts AS c
LEFT JOIN dbo.cart_items AS ci ON ci.cart_id = c.id
LEFT JOIN dbo.products AS p ON p.id = ci.product_id
WHERE c.id = @cart_id
ORDER BY ci.added_at;

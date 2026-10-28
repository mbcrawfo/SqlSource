-- name: DeleteAbandonedCartItems
-- param: @created_before datetime2(0)
DELETE ci
OUTPUT deleted.*, c.customer_id
FROM dbo.cart_items AS ci
JOIN dbo.carts AS c ON c.id = ci.cart_id
WHERE c.checked_out_at IS NULL
    AND c.created_at < @created_before;

-- name: GetCustomerOrderSummary
-- param: @customer_id int
SELECT
    COUNT(*) AS order_count,
    MIN(o.placed_at) AS first_ordered_at,
    MAX(o.placed_at) AS last_ordered_at,
    SUM(CASE WHEN o.shipped_at IS NULL THEN 1 ELSE 0 END) AS unshipped_count
FROM dbo.orders AS o
WHERE o.customer_id = @customer_id;

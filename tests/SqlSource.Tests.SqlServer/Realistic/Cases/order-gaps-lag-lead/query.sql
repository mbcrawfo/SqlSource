-- name: ListCustomerOrderGaps
-- param: @customer_id int
SELECT
    o.id,
    o.placed_at,
    o.status,
    LAG(o.placed_at) OVER (ORDER BY o.placed_at, o.id) AS previous_placed_at,
    LEAD(o.placed_at) OVER (ORDER BY o.placed_at, o.id) AS next_placed_at,
    DATEDIFF(day, LAG(o.placed_at) OVER (ORDER BY o.placed_at, o.id), o.placed_at) AS days_since_previous,
    LAG(o.status, 1, 'none') OVER (ORDER BY o.placed_at, o.id) AS previous_status
FROM dbo.orders AS o
WHERE o.customer_id = @customer_id
ORDER BY o.placed_at, o.id;

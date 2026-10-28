-- name: ListTopCustomers
SELECT
    a.id,
    a.name,
    count(DISTINCT o.id) AS order_count,
    sum(l.quantity * l.unit_price) AS revenue,
    max(o.placed_at) AS last_placed_at,
    max(o.coupon) AS coupon
FROM accounts a
JOIN orders o ON o.account_id = a.id
JOIN order_lines l ON l.order_id = o.id
GROUP BY a.id, a.name
HAVING sum(l.quantity * l.unit_price) >= @min_revenue
ORDER BY revenue DESC;

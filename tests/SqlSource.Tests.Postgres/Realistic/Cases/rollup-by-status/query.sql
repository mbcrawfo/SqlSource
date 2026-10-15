-- name: GetRevenueByStatusAndMonth
SELECT
    a.status,
    date_trunc('month', o.placed_at) AS month,
    count(DISTINCT o.id) AS orders,
    sum(l.quantity * l.unit_price) AS revenue,
    grouping(a.status, date_trunc('month', o.placed_at)) AS level
FROM orders o
JOIN accounts a ON a.id = o.account_id
LEFT JOIN order_lines l ON l.order_id = o.id
GROUP BY ROLLUP (a.status, date_trunc('month', o.placed_at))
ORDER BY a.status NULLS LAST, month NULLS LAST;

-- name: ListMonthlySpendWithChange
WITH monthly AS (
    SELECT o.account_id, date_trunc('month', o.placed_at) AS month_start, sum(l.quantity * l.unit_price) AS total
    FROM orders o
    JOIN order_lines l ON l.order_id = o.id
    WHERE o.placed_at >= @from
    GROUP BY o.account_id, date_trunc('month', o.placed_at)
)
SELECT
    a.id,
    a.name,
    m.month_start,
    m.total,
    m.total - lag(m.total) OVER (PARTITION BY a.id ORDER BY m.month_start) AS change
FROM accounts a
JOIN monthly m ON m.account_id = a.id
ORDER BY a.id, m.month_start;

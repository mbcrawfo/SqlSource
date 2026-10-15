-- name: ListOrderHistoryWithRunningTotals
WITH order_totals AS (
    SELECT o.id, o.account_id, o.placed_at, sum(l.quantity * l.unit_price) AS total
    FROM orders o
    JOIN order_lines l ON l.order_id = o.id
    GROUP BY o.id
)
SELECT
    ot.id,
    ot.account_id,
    ot.placed_at,
    ot.total,
    sum(ot.total) OVER w AS running_total,
    lag(ot.total) OVER w AS previous_total,
    ot.total - lag(ot.total) OVER w AS change,
    rank() OVER (ORDER BY ot.total DESC) AS revenue_rank,
    count(*) OVER (PARTITION BY ot.account_id) AS account_orders
FROM order_totals ot
WINDOW w AS (PARTITION BY ot.account_id ORDER BY ot.placed_at)
ORDER BY ot.account_id, ot.placed_at;

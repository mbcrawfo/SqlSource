-- name: ListCustomerLifetimeValue
WITH line_totals AS (
    SELECT l.order_id, sum(l.quantity * l.unit_price) AS order_total
    FROM order_lines l
    GROUP BY l.order_id
),
order_totals AS (
    SELECT o.account_id, o.id AS order_id, o.placed_at, coalesce(lt.order_total, 0) AS order_total
    FROM orders o
    LEFT JOIN line_totals lt ON lt.order_id = o.id
),
account_totals AS (
    SELECT
        ot.account_id,
        count(*) AS order_count,
        sum(ot.order_total) AS lifetime,
        max(ot.placed_at) AS last_placed_at
    FROM order_totals ot
    GROUP BY ot.account_id
)
SELECT a.id, a.name, t.order_count, t.lifetime, t.last_placed_at, a.closed_at
FROM accounts a
JOIN account_totals t ON t.account_id = a.id
ORDER BY t.lifetime DESC, a.id;

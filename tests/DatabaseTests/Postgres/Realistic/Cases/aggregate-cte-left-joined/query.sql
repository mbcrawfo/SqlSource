-- name: ListAccountSpending
WITH totals AS (
    SELECT o.account_id, sum(l.quantity * l.unit_price) AS spent, count(*) AS line_count
    FROM orders o
    JOIN order_lines l ON l.order_id = o.id
    GROUP BY o.account_id
)
SELECT a.id, a.name, a.email, coalesce(t.spent, 0) AS spent, t.line_count, t.account_id
FROM accounts a
LEFT JOIN totals t ON t.account_id = a.id
WHERE a.closed_at IS NULL
ORDER BY spent DESC
LIMIT @limit OFFSET @offset;

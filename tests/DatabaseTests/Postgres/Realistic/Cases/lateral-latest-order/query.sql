-- name: ListAccountsWithLatestOrder
SELECT a.id, a.name, lo.order_id, lo.placed_at, lo.note, lo.line_count
FROM accounts a
CROSS JOIN LATERAL (
    SELECT
        o.id AS order_id,
        o.placed_at,
        o.note,
        (SELECT count(*) FROM order_lines l WHERE l.order_id = o.id) AS line_count
    FROM orders o
    WHERE o.account_id = a.id
    ORDER BY o.placed_at DESC
    LIMIT 1
) lo
ORDER BY lo.placed_at DESC;

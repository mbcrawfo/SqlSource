-- name: ListOrdersWithLaterOrders
WITH recent AS (
    SELECT o.id, o.account_id, o.placed_at, o.note
    FROM orders o
    WHERE o.placed_at >= @since
)
SELECT r.id, r.account_id, r.placed_at, r.note, n.id AS later_id, n.placed_at AS later_placed_at
FROM recent r
LEFT JOIN recent n ON n.account_id = r.account_id AND n.placed_at > r.placed_at
ORDER BY r.account_id, r.placed_at, n.placed_at;

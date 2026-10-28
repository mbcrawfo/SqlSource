-- name: ListAccountsWithOrderStats
SELECT a.id, a.name, s.order_count, s.last_placed_at
FROM accounts a
LEFT JOIN LATERAL (
    SELECT count(*) AS order_count, max(o.placed_at) AS last_placed_at
    FROM orders o
    WHERE o.account_id = a.id
    GROUP BY o.account_id
) s ON true
ORDER BY a.id;

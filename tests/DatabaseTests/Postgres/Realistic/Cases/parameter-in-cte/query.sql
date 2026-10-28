-- name: ListRecentBuyersWithLastLogin
WITH recent_orders AS (
    SELECT o.account_id, count(*) AS order_count, max(o.placed_at) AS last_placed_at
    FROM orders o
    WHERE o.placed_at >= @since
    GROUP BY o.account_id
)
SELECT
    a.id,
    a.name,
    ro.order_count,
    ro.last_placed_at,
    ll.at AS last_login_at,
    ll.device AS last_login_device
FROM recent_orders ro
JOIN accounts a ON a.id = ro.account_id
LEFT JOIN LATERAL (
    SELECT e.at, e.payload ->> 'device' AS device
    FROM events e
    WHERE e.account_id = a.id AND e.kind = 'login'
    ORDER BY e.at DESC
    LIMIT 1
) ll ON true
ORDER BY ro.last_placed_at DESC;

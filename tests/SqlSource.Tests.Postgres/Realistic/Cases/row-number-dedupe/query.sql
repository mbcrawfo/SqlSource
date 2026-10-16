-- name: ListLatestOrderPerOpenAccount
SELECT s.account_id, s.name, s.order_id, s.placed_at, s.shipped_at
FROM (
    SELECT
        a.id AS account_id,
        a.name,
        o.id AS order_id,
        o.placed_at,
        o.shipped_at,
        row_number() OVER (PARTITION BY a.id ORDER BY o.placed_at DESC) AS rn
    FROM accounts a
    LEFT JOIN orders o ON o.account_id = a.id
    WHERE a.status <> 'closed'
) s
WHERE s.rn = 1
ORDER BY s.name;

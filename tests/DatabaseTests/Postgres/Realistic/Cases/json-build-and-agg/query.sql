-- name: ListAccountsWithOrdersAsJson
SELECT
    a.id,
    jsonb_build_object('id', a.id, 'name', a.name, 'email', a.email) AS account,
    coalesce(
        json_agg(
            json_build_object('id', o.id, 'placed_at', o.placed_at, 'note', o.note) ORDER BY o.placed_at
        ) FILTER (WHERE o.id IS NOT NULL),
        '[]'
    ) AS orders,
    json_agg(o.coupon) FILTER (WHERE o.coupon IS NOT NULL) AS coupons,
    count(o.id) AS order_count
FROM accounts a
LEFT JOIN orders o ON o.account_id = a.id
GROUP BY a.id
ORDER BY a.id;

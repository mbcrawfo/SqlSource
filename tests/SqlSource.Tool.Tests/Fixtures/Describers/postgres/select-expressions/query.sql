-- name: CountOrders
SELECT
    user_id,
    count(*) AS orders,
    upper(max(o.note)) AS loudest,
    sum(o.total)::numeric(20, 4) AS spent,
    'x' AS mark,
    NULL AS nothing,
    1
FROM orders AS o
JOIN bookings AS b USING (user_id)
GROUP BY user_id;

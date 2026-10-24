-- name: OrderSummaries
-- param: @minimum_total decimal(18,2)
SELECT
    o.user_id AS user_id,
    COUNT(*) AS order_count,
    SUM(o.total) AS total_spent,
    MAX(o.total * u.credit) AS largest_weighted,
    COALESCE(u.nickname, u.name) AS shown_name,
    CAST(o.user_id AS bigint) AS user_key,
    RANK() OVER (ORDER BY COUNT(*) DESC) AS order_rank
FROM dbo.orders AS o
JOIN dbo.users AS u ON u.id = o.user_id
WHERE o.total >= @minimum_total
GROUP BY o.user_id, u.nickname, u.name;

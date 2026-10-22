-- name: OrderTotals
SELECT user_id, COUNT(*) AS order_count, MAX(total) AS max_total
FROM dbo.orders
GROUP BY user_id;

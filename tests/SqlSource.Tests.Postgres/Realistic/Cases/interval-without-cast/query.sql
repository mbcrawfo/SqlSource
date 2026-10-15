-- name: ListRecentOrders
SELECT o.id, o.account_id, o.placed_at, o.shipped_at
FROM orders o
WHERE o.placed_at > now() - @age
ORDER BY o.placed_at DESC;

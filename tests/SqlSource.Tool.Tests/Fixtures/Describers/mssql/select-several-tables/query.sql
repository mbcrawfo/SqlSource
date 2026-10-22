-- name: OrdersWithOwner
SELECT o.id, o.total, u.name
FROM dbo.orders AS o
JOIN dbo.users AS u ON u.id = o.user_id;

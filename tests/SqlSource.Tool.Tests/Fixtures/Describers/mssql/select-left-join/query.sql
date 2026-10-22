-- name: UsersWithOrders
SELECT u.id AS user_id, u.name, o.id AS order_id, o.total
FROM dbo.users AS u
LEFT JOIN dbo.orders AS o ON o.user_id = u.id;

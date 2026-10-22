-- name: OrdersOfEveryUser
SELECT o.id, o.user_id, o.total, o.note
FROM dbo.users AS u
LEFT JOIN dbo.orders AS o ON o.user_id = u.id;

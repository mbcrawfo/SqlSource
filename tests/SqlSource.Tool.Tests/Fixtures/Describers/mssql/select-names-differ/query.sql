-- name: OrdersRenamed
SELECT id, user_id AS owner_id, total, note FROM dbo.orders;

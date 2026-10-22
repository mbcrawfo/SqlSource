-- name: GetOrders
SELECT id, total, note FROM dbo.orders WHERE user_id = @userId;

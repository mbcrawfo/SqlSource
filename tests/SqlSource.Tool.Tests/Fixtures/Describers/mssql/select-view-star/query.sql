-- name: UserOrders
SELECT * FROM dbo.user_orders WHERE user_id = @userId;

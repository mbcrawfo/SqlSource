-- name: PurgeOrders
-- param: @tenant int
DELETE FROM dbo.orders WHERE user_id = @userId {{filter:AND note IS NOT NULL}};

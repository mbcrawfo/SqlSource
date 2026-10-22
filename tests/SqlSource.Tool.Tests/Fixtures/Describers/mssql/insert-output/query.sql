-- name: AddOrder
INSERT INTO dbo.orders (user_id, total, note)
OUTPUT inserted.*
VALUES (@userId, @total, @note);

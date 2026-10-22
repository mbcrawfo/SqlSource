-- name: PriceHistory
SELECT id, amount FROM dbo.prices FOR SYSTEM_TIME ALL WHERE id = @id;

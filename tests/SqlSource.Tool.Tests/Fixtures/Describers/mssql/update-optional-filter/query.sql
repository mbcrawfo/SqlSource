-- name: MarkByName
UPDATE dbo.users SET nickname = N'x' WHERE (@name IS NULL OR name = @name);

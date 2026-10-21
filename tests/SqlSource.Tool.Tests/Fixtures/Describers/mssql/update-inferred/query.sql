-- name: RenameUser
UPDATE dbo.users SET name = @name, email = @email WHERE id = @id;

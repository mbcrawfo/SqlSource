-- name: RenameUser
UPDATE users SET name = @name WHERE id = @id;

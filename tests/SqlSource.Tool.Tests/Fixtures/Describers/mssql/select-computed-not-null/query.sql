-- name: UserById
SELECT id, name, email, nickname, credit, created_at, deleted_at, name_len, name_len_p, rv
FROM dbo.users
WHERE id = @id;

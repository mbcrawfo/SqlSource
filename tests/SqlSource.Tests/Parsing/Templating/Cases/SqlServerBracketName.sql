-- dialect: mssql
-- name: Bracketed
SELECT [a--b], [c]]--d], {{col:name}} FROM users WHERE id = @id;

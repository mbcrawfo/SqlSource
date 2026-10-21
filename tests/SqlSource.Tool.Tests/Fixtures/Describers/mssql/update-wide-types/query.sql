-- name: ClearNicknames
UPDATE TOP (@n) dbo.users SET nickname = NULL WHERE name LIKE @pattern;

-- dialect: mysql,no_backslash_escapes
-- name: Plain
SELECT 'a\', {{col:name}}, "b\" FROM users;

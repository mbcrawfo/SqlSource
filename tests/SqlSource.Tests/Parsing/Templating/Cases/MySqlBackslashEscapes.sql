-- dialect: mysql
-- A token inside a quoted region is still a token.
-- name: Escaped
SELECT 'it\'s {{q}}', "a\" -- {{q}}", {{col:name}} FROM users;

-- dialect: mysql
-- name: Escaped
SELECT 'it\'s {{no}}', "a\" -- {{no}}", {{col:name}} FROM users;

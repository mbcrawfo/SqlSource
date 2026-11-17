-- dialect: postgres
-- name: Greeting
-- token-ignore: name
SELECT 'Hello, {{name}}' AS greeting FROM {{t:users}};

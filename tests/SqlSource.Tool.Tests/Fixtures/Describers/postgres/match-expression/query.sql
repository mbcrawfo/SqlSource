-- name: ListUsersShouted
SELECT *, upper(name) AS shouted FROM users;

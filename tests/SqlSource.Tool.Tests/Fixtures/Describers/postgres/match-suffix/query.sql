-- name: ListUsersWithASuffix
SELECT id, name AS "name!", email, status, tags, balance, created_at, deleted_at, name_upper FROM users;

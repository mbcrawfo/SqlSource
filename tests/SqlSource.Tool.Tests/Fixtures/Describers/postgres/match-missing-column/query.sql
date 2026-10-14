-- name: ListUsersWithoutTheLast
SELECT id, name, email, status, tags, balance, created_at, deleted_at FROM users;

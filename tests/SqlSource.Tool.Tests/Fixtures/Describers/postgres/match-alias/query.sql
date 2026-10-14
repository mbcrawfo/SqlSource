-- name: ListUsersRenamed
SELECT id, name AS user_name, email, status, tags, balance, created_at, deleted_at, name_upper FROM users;

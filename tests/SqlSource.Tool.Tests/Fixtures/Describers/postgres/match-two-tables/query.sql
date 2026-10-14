-- name: ListUsersWithOrders
SELECT u.*, o.* FROM users AS u JOIN orders AS o ON o.user_id = u.id;

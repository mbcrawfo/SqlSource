-- name: ListOrderUsers
SELECT u.* FROM orders AS o LEFT JOIN users AS u ON u.id = o.user_id;

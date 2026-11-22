-- name: GetUsersWithOrderStats
SELECT
    u.id,
    (SELECT count(*) FROM bookings) AS booking_count,
    (SELECT max(o.total) FROM orders o WHERE o.user_id = u.id) AS biggest_order
FROM users u
WHERE u.id > @min_id;

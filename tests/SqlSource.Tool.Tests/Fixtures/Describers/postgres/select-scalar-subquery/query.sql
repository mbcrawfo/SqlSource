-- name: GetUsersWithOrderStats
SELECT
    u.id,
    (SELECT max(o.total) FROM orders o WHERE o.user_id = u.id) AS biggest_order,
    (SELECT count(*) FROM bookings) AS booking_count
FROM users u
WHERE u.id > @min_id;

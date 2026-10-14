-- name: WalkNatural
SELECT id, user_id, o.total, b.during
FROM orders o NATURAL FULL JOIN bookings b;

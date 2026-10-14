-- name: WalkUsing
SELECT user_id, o.total, b.during
FROM orders o FULL JOIN bookings b USING (user_id);

-- name: WalkSemiAnti
SELECT u.id, left(u.name, 1) AS initial
FROM users u
WHERE EXISTS (SELECT 1 FROM orders o WHERE o.user_id = u.id)
    AND NOT EXISTS (SELECT 1 FROM bookings b WHERE b.user_id = u.id);

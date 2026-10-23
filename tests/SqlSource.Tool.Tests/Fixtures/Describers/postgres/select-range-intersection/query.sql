-- name: GetBookingOverlap
SELECT b.id, b.during * tstzrange(@from, @to) AS overlap
FROM bookings b
WHERE b.id = @id;

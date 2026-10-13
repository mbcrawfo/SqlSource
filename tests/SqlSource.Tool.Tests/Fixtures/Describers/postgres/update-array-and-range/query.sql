-- name: MoveBooking
UPDATE bookings SET during = @during, tags = @tags WHERE id = @id;

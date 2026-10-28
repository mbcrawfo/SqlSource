-- name: CancelReservationsOfStatus
DELETE FROM reservations r
USING accounts a
WHERE a.id = r.account_id AND a.status = @status
RETURNING r.id, r.account_id, r.during, a.name, a.email;

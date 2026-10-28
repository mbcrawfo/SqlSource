-- name: ListReservationCoverage
SELECT
    coalesce(a.id, r.account_id) AS account_id,
    a.name,
    a.email,
    r.id AS reservation_id,
    r.during,
    upper(r.during) - lower(r.during) AS duration
FROM (SELECT id, name, email FROM accounts WHERE status = 'active') a
FULL JOIN reservations r ON r.account_id = a.id
ORDER BY account_id, reservation_id;

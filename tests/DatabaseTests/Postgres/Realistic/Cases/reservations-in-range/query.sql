-- name: ListReservationsOverlapping
SELECT
    r.id,
    a.name AS account_name,
    r.during,
    upper(r.during) AS ends_at,
    r.during * tstzrange(@from, @to) AS overlap
FROM reservations r
JOIN accounts a ON a.id = r.account_id
WHERE r.during && tstzrange(@from, @to)
ORDER BY lower(r.during), r.id;

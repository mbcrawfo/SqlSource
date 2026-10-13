-- name: GetEverything
SELECT
    u.id, u.name, u.email, u.status, u.tags, u.balance, u.created_at, u.deleted_at, u.name_upper,
    s.flag, s.small, s.big, s.ratio, s.amount, s.code, s.label, s.born, s.at_time, s.at_local, s.span, s.token,
    s.payload, s.document,
    b.during, b.slots
FROM users AS u
CROSS JOIN samples AS s
CROSS JOIN bookings AS b
WHERE u.id = @id;

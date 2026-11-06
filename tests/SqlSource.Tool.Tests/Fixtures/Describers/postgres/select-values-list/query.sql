-- name: GetUsersWithLabels
SELECT u.id, u.name, v.label
FROM users u JOIN (VALUES (1, 'a'), (2, 'b')) AS v(id, label) ON v.id = u.id
WHERE u.id > @min_id;

-- name: WalkSelfJoin
SELECT a.id, a.name, b.id AS other_id, b.name AS other_name
FROM users a LEFT JOIN users b ON b.name = a.name AND b.id <> a.id;

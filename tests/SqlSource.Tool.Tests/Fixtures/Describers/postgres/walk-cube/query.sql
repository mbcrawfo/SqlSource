-- name: WalkCube
SELECT u.status, u.name, count(*) AS n
FROM users u
GROUP BY CUBE (u.status, u.name);

-- name: WalkGroupingSets
SELECT u.status, count(*) AS n
FROM users u
GROUP BY GROUPING SETS ((u.status), ());

-- name: WalkRollup
SELECT u.status, count(*) AS n
FROM users u
GROUP BY ROLLUP (u.status);

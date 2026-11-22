-- name: WalkScanColumn
SELECT
    s.n,
    s.last_note
FROM (
    SELECT
        o.user_id,
        count(*) AS n,
        max(o.note) AS last_note
    FROM orders o
    GROUP BY o.user_id
    OFFSET 0
) s
WHERE s.n > 0;

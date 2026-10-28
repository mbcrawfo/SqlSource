-- name: ListCustomersOutsideTimeZone
-- param: @time_zone varchar(50) null
SELECT
    c.id,
    c.name,
    c.email,
    c.time_zone,
    c.closed_at
FROM dbo.customers AS c
WHERE c.time_zone IS DISTINCT FROM @time_zone
ORDER BY c.name;

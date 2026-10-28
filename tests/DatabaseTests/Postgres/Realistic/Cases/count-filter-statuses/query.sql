-- name: GetAccountStatusSummary
SELECT
    count(*) AS total,
    count(*) FILTER (WHERE a.status = 'active') AS active,
    count(*) FILTER (WHERE a.status <> 'active') AS inactive,
    count(a.email) AS with_email,
    max(a.created_at) AS newest_created_at,
    avg(cardinality(a.tags)) FILTER (WHERE a.status = 'active') AS avg_active_tags
FROM accounts a
WHERE a.created_at >= @since;

-- name: ListAccountProfiles
SELECT
    a.id,
    coalesce(a.email, 'no email') AS contact,
    a.email::text AS raw_email,
    CASE WHEN a.closed_at IS NULL THEN 'open' ELSE 'closed' END AS state,
    CASE a.status WHEN 'active' THEN 'Active' WHEN 'suspended' THEN 'On hold' END AS status_label,
    greatest(a.created_at, a.closed_at) AS last_change,
    a.settings IS NULL AS settings_missing,
    coalesce(a.settings ->> 'theme', 'system') AS theme,
    a.settings -> 'limits' ->> 'daily' AS daily_limit,
    'v1' AS profile_version
FROM accounts a
ORDER BY a.id;

-- name: ListCustomerContacts
SELECT
    c.id,
    c.name,
    ISNULL(c.email, N'') AS email,
    COALESCE(c.company, c.name) AS display_name,
    COALESCE(c.email, c.phone) AS reach_at,
    NULLIF(c.phone, '') AS phone,
    NULLIF(TRIM(c.company), N'') AS company,
    IIF(c.email IS NOT NULL, 'email', IIF(c.phone IS NOT NULL, 'phone', 'none')) AS preferred_channel,
    ISNULL(c.time_zone, 'UTC') AS time_zone
FROM dbo.customers AS c
ORDER BY display_name, c.id;

-- name: FindEventsByPayload
SELECT
    e.id,
    e.at,
    e.kind,
    a.name AS account_name,
    e.payload ->> 'device' AS device,
    jsonb_array_length(e.payload -> 'items') AS item_count
FROM events e
LEFT JOIN accounts a ON a.id = e.account_id
WHERE e.payload @> @filter AND e.payload ? @key
ORDER BY e.at;

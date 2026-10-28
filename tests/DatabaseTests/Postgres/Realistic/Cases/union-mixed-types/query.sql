-- name: ListItemsNeedingAttention
SELECT a.id, a.name AS label, a.created_at AS since, 'account' AS kind
FROM accounts a
WHERE a.status = 'suspended'
UNION ALL
SELECT o.id, o.note, o.placed_at, 'order'
FROM orders o
WHERE o.shipped_at IS NULL
ORDER BY since;

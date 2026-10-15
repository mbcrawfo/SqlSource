-- name: SearchEverything
SELECT a.id, a.name AS label, 'account' AS kind
FROM accounts a
WHERE a.name ILIKE @pattern
UNION
SELECT o.id, o.note, 'order'
FROM orders o
WHERE o.note ILIKE @pattern
UNION
SELECT p.id, p.name, 'product'
FROM products p
WHERE p.name ILIKE @pattern OR p.sku ILIKE @pattern
ORDER BY label, kind
LIMIT 20;

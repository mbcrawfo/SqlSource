-- name: ListAccountTags
SELECT a.id, a.name, t.tag, t.ordinal, count(*) OVER (PARTITION BY t.tag) AS accounts_with_tag
FROM accounts a
CROSS JOIN LATERAL unnest(a.tags) WITH ORDINALITY AS t(tag, ordinal)
WHERE a.status <> 'closed'
ORDER BY a.id, t.ordinal;

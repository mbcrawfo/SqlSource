-- name: ListAccountsByLastOrder
SELECT
    a.id,
    a.name,
    a.status,
    (SELECT max(o.placed_at) FROM orders o WHERE o.account_id = a.id) AS last_placed_at,
    (SELECT o.note FROM orders o WHERE o.account_id = a.id ORDER BY o.placed_at LIMIT 1) AS first_note,
    (SELECT count(*) FROM orders o WHERE o.account_id = a.id) AS order_count
FROM accounts a
WHERE a.status <> 'closed'
ORDER BY last_placed_at DESC NULLS LAST, a.id;

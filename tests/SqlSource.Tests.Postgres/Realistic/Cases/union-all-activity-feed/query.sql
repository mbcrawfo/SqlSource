-- name: ListAccountActivity
-- param: @account_id int
SELECT 'order' AS kind, o.id AS ref_id, o.placed_at AS at, o.note AS detail
FROM orders o
WHERE o.account_id = @account_id
UNION ALL
SELECT 'event', e.id, e.at, e.payload ->> 'ip'
FROM events e
WHERE e.account_id = @account_id
UNION ALL
SELECT 'invoice', i.id, i.issued_on::timestamptz, i.paid_on::text
FROM billing.invoices i
JOIN orders o ON o.id = i.order_id
WHERE o.account_id = @account_id
ORDER BY at DESC
LIMIT 50;

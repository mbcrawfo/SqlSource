-- name: GetCustomerActivity
-- param: @customer_id int
SELECT
    'order' AS kind,
    o.id AS reference_id,
    o.placed_at AS occurred_at,
    (
        SELECT SUM(ol.quantity * ol.unit_price)
        FROM dbo.order_lines AS ol
        WHERE ol.order_id = o.id
    ) AS amount,
    o.status AS detail,
    o.note
FROM dbo.orders AS o
WHERE o.customer_id = @customer_id
UNION ALL
SELECT
    'payment',
    p.id,
    p.paid_at,
    p.amount,
    p.method,
    p.reference
FROM billing.payments AS p
JOIN billing.invoices AS i ON i.id = p.invoice_id
JOIN dbo.orders AS o ON o.id = i.order_id
WHERE o.customer_id = @customer_id
ORDER BY occurred_at DESC, kind;

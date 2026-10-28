-- name: ListCustomersWithLastPayment
SELECT
    c.id,
    c.name,
    c.status,
    (
        SELECT MAX(p.paid_at)
        FROM billing.payments AS p
        JOIN billing.invoices AS i ON i.id = p.invoice_id
        JOIN dbo.orders AS o ON o.id = i.order_id
        WHERE o.customer_id = c.id
    ) AS last_paid_at,
    (
        SELECT COUNT(*)
        FROM dbo.orders AS o
        WHERE o.customer_id = c.id
    ) AS order_count
FROM dbo.customers AS c
ORDER BY last_paid_at DESC, c.id;

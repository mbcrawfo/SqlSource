-- name: ListUnpaidInvoices
WITH unpaid AS MATERIALIZED (
    SELECT i.order_id, i.amount, i.issued_on
    FROM billing.invoices i
    WHERE i.paid_on IS NULL
)
SELECT o.id AS order_id, o.account_id, a.name, u.amount, u.issued_on, o.shipped_at
FROM unpaid u
JOIN orders o ON o.id = u.order_id
JOIN accounts a ON a.id = o.account_id
ORDER BY u.issued_on;

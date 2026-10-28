-- name: ListInvoicesWithLatestPayment
SELECT
    i.id,
    i.number,
    cu.name AS customer_name,
    i.issued_at,
    i.due_at,
    i.amount,
    lp.paid_at AS last_paid_at,
    lp.amount AS last_paid_amount,
    lp.method AS last_method,
    lp.reference AS last_reference
FROM billing.invoices AS i
JOIN dbo.orders AS o ON o.id = i.order_id
JOIN dbo.customers AS cu ON cu.id = o.customer_id
OUTER APPLY (
    SELECT TOP (1) p.paid_at, p.amount, p.method, p.reference
    FROM billing.payments AS p
    WHERE p.invoice_id = i.id
    ORDER BY p.paid_at DESC, p.id DESC
) AS lp
WHERE i.voided_at IS NULL
ORDER BY i.issued_at, i.id;

-- name: ListCustomersLastOrderInvoice
SELECT
    clo.customer_id,
    clo.name,
    c.email,
    clo.order_id,
    clo.placed_at AS last_ordered_at,
    clo.status AS last_order_status,
    i.number AS invoice_number,
    i.amount AS invoice_amount
FROM dbo.customer_last_orders AS clo
JOIN dbo.customers AS c ON c.id = clo.customer_id
LEFT JOIN billing.invoices AS i ON i.order_id = clo.order_id
ORDER BY clo.placed_at DESC, clo.customer_id;

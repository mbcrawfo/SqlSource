-- name: ListOrdersWithInvoice
SELECT o.id, o.placed_at, o.status, o.ship_city, i.number AS invoice_number, i.issued_at, i.due_at, i.amount
FROM dbo.orders AS o
LEFT JOIN billing.invoices AS i ON i.order_id = o.id
WHERE o.customer_id = @customer_id
ORDER BY o.placed_at, o.id;

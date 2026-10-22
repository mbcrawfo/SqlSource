-- name: ListCustomerPayments
-- param: @customer_id int
SELECT
    p.id,
    p.paid_at,
    i.number AS invoice_number,
    p.method,
    p.reference,
    p.amount,
    SUM(p.amount) OVER (ORDER BY p.paid_at, p.id ROWS UNBOUNDED PRECEDING) AS paid_to_date
FROM billing.payments AS p
JOIN billing.invoices AS i ON i.id = p.invoice_id
JOIN dbo.orders AS o ON o.id = i.order_id
WHERE o.customer_id = @customer_id
ORDER BY p.paid_at, p.id;

-- name: RecalculateInvoicesSince
UPDATE billing.invoices i
SET amount = s.total
FROM (
    SELECT
        l.order_id,
        sum(l.quantity * l.unit_price) AS total,
        count(*) AS line_count,
        max(p.discontinued_on) AS last_discontinued_on
    FROM order_lines l
    JOIN products p ON p.id = l.product_id
    GROUP BY l.order_id
) s
WHERE s.order_id = i.order_id AND i.issued_on >= @since
RETURNING i.id, i.order_id, i.amount, i.issued_on, s.total, s.line_count, s.last_discontinued_on;

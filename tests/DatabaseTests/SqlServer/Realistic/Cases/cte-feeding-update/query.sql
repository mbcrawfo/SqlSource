-- name: FlagOrdersWithOverdueInvoices
-- param: @as_of date
WITH due AS (
    SELECT
        i.order_id,
        i.number,
        i.due_at,
        i.amount - ISNULL(SUM(p.amount), 0) AS outstanding
    FROM billing.invoices AS i
    LEFT JOIN billing.payments AS p ON p.invoice_id = i.id
    WHERE i.voided_at IS NULL
        AND i.due_at < @as_of
    GROUP BY i.order_id, i.number, i.due_at, i.amount
    HAVING i.amount > ISNULL(SUM(p.amount), 0)
)
UPDATE o
SET o.note = CONCAT(o.note + N'; ', N'Invoice ', d.number, N' overdue since ', CONVERT(char(10), d.due_at, 23))
OUTPUT
    inserted.id,
    inserted.status,
    deleted.note AS previous_note,
    inserted.note,
    d.number AS invoice_number,
    d.due_at,
    d.outstanding,
    inserted.shipped_at
FROM dbo.orders AS o
JOIN due AS d ON d.order_id = o.id;

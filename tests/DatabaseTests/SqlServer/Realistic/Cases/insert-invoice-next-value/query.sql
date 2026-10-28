-- name: IssueInvoiceForOrder
-- param: @order_id int
-- param: @issued_at date
-- param: @terms_days int null
-- param: @tax_rate decimal(5, 4) null
INSERT INTO billing.invoices (number, order_id, issued_at, due_at, amount, tax)
OUTPUT
    inserted.id,
    inserted.number,
    inserted.order_id,
    inserted.issued_at,
    inserted.due_at,
    inserted.amount,
    inserted.tax,
    inserted.voided_at
SELECT
    NEXT VALUE FOR billing.invoice_numbers,
    o.id,
    @issued_at,
    DATEADD(day, @terms_days, @issued_at),
    t.total,
    ROUND(t.total * @tax_rate, 2)
FROM dbo.orders AS o
CROSS APPLY (
    SELECT SUM(ol.quantity * ol.unit_price) AS total
    FROM dbo.order_lines AS ol
    WHERE ol.order_id = o.id
) AS t
WHERE o.id = @order_id
    AND t.total IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM billing.invoices AS i WHERE i.order_id = o.id);

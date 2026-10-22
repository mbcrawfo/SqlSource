-- name: ListBillingActivity
-- param: @from date
-- param: @to date
WITH issued AS (
    SELECT i.id, i.number, i.issued_at, i.amount
    FROM billing.invoices AS i
    WHERE i.issued_at >= @from
        AND i.issued_at < @to
        AND i.voided_at IS NULL
),
received AS (
    SELECT p.id, p.invoice_id, p.paid_at, p.amount, p.method
    FROM billing.payments AS p
    WHERE p.paid_at >= @from
        AND p.paid_at < @to
)
SELECT
    COALESCE(iss.id, rec.invoice_id) AS invoice_id,
    iss.number AS invoice_number,
    iss.issued_at,
    iss.amount AS invoiced,
    rec.id AS payment_id,
    rec.paid_at,
    rec.amount AS paid,
    rec.method
FROM issued AS iss
FULL JOIN received AS rec ON rec.invoice_id = iss.id
ORDER BY COALESCE(iss.issued_at, CAST(rec.paid_at AS date)), invoice_id, rec.paid_at;

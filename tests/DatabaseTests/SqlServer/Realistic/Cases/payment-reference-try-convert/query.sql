-- name: ListPaymentsForReconciliation
-- param: @from datetime2(0)
-- param: @to datetime2(0)
SELECT
    p.id,
    i.number AS invoice_number,
    p.paid_at,
    p.amount,
    p.method,
    p.reference,
    TRY_CONVERT(int, p.reference) AS bank_reference
FROM billing.payments AS p
JOIN billing.invoices AS i ON i.id = p.invoice_id
WHERE p.paid_at >= @from
    AND p.paid_at < @to
ORDER BY p.paid_at, p.id;

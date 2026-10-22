-- name: ListInvoicesDueInMonth
-- param: @year int
-- param: @month int
-- param: @reminder_days int
SELECT
    i.id,
    i.number,
    i.issued_at,
    i.due_at,
    COALESCE(i.due_at, i.issued_at) AS due_on,
    DATEADD(day, @reminder_days, i.due_at) AS remind_on,
    EOMONTH(COALESCE(i.due_at, i.issued_at)) AS statement_on,
    i.amount
FROM billing.invoices AS i
WHERE i.voided_at IS NULL
    AND COALESCE(i.due_at, i.issued_at) >= DATEFROMPARTS(@year, @month, 1)
    AND COALESCE(i.due_at, i.issued_at) <= EOMONTH(DATEFROMPARTS(@year, @month, 1))
ORDER BY due_on, i.id;

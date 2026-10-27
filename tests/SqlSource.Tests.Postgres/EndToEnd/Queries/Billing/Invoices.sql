-- The queries of the database billing, claimed by InvoiceQueries.  The folder's settings file gives them their model
-- shape, their output suffix, their type override and their collection type.
-- database: billing
-- model-namespace: SqlSource.Tests.Postgres.EndToEnd.Billing

-- name: ListInvoices
-- summary: Every invoice, by SELECT *.
SELECT *
FROM billing.invoices
ORDER BY id;

-- name: GetInvoice
-- summary: The number and payment of one invoice.
SELECT id, number, amount, paid_at
FROM billing.invoices
WHERE id = @invoiceId;

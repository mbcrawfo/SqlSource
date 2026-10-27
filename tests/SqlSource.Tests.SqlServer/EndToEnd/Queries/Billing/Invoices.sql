-- The queries of the database billing, claimed by InvoiceQueries.  The folder's settings file gives them their model
-- shape, their output suffix and their type override.
-- database: billing
-- model-namespace: SqlSource.Tests.SqlServer.EndToEnd.Billing

-- name: ListInvoices
-- summary: Every invoice, by SELECT *, in the model of the table, whose name has its schema billing.
SELECT *
FROM billing.invoices
ORDER BY id;

-- name: GetInvoice
-- summary: The number and payment of one invoice.
SELECT id, number, amount, paid_at
FROM billing.invoices
WHERE id = @invoiceId;

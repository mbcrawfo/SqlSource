-- The queries of the database billing, claimed by InvoiceQueries.  The project's metadata gives this folder its model
-- shape, its output suffix and its type override.
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

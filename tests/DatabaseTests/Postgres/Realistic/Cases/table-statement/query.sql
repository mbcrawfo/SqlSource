-- name: ListLatestInvoices
TABLE billing.invoices
ORDER BY issued_on DESC
LIMIT @limit;

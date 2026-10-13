-- name: ListInvoices
SELECT id, order_id, amount, issued_on FROM billing.invoices WHERE order_id = @order_id;

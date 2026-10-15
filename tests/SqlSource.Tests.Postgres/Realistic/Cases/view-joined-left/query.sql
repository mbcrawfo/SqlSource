-- name: ListAccountOrdersWithInvoice
SELECT v.account_id, v.name, v.order_id, v.placed_at, i.amount, i.paid_on
FROM account_orders v
LEFT JOIN billing.invoices i ON i.order_id = v.order_id
ORDER BY v.account_id, v.placed_at;

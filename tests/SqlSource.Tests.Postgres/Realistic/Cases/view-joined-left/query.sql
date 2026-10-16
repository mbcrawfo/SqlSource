-- name: ListOpenAccountOrdersWithInvoice
SELECT v.account_id, v.name, a.status, v.order_id, v.placed_at, i.amount, i.paid_on
FROM account_orders v
JOIN accounts a ON a.id = v.account_id
LEFT JOIN billing.invoices i ON i.order_id = v.order_id
WHERE a.closed_at IS NULL AND a.created_at >= @since
ORDER BY v.account_id, v.placed_at;

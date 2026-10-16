-- name: ListInvoicedBuyers
-- param: @min_amount numeric
SELECT a.id, a.name, a.email, a.settings ->> 'theme' AS theme
FROM accounts a
WHERE a.id IN (
    SELECT o.account_id
    FROM orders o
    JOIN billing.invoices i ON i.order_id = o.id
    WHERE i.amount >= @min_amount
)
ORDER BY a.name
LIMIT @limit;

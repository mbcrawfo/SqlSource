-- name: GetOrderReport
SELECT
    o.id AS order_id,
    o.placed_at,
    o.note,
    a.name AS account_name,
    l.line_no,
    p.sku,
    p.name AS product_name,
    c.name AS category,
    l.quantity * l.unit_price AS line_total,
    i.amount AS invoiced,
    i.paid_on
FROM orders o
JOIN accounts a ON a.id = o.account_id
JOIN order_lines l ON l.order_id = o.id
JOIN products p ON p.id = l.product_id
LEFT JOIN categories c ON c.id = p.category_id
LEFT JOIN billing.invoices i ON i.order_id = o.id
WHERE o.placed_at >= @from AND o.placed_at < @to
ORDER BY o.id, l.line_no;

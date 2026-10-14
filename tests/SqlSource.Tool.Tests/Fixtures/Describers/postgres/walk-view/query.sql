-- name: WalkView
SELECT v.user_id, v.name, v.order_id, v.total, i.amount
FROM user_orders v LEFT JOIN billing.invoices i ON i.order_id = v.order_id;

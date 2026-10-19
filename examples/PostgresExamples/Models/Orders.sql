-- The queries of the orders.  Their output is models, set by the attribute of StoreQueries, so each gets a model of
-- its parameters and one of its rows, from its entry in Orders.sql.json.  The parameters are in camelCase, which Dapper
-- binds to the properties of the input model.

-- name: GetOrder
-- summary: One order and the name of its customer.
SELECT o.id, o.status, o.placed_at, o.shipped_at, u.name AS customer_name
FROM orders AS o
JOIN users AS u ON u.id = o.user_id
WHERE o.id = @orderId;

-- name: ListOrderTotals
-- summary: The total of each order.
-- column: total not null
SELECT o.id, sum(l.quantity * l.unit_price) AS total
FROM orders AS o
JOIN order_lines AS l ON l.order_id = o.id
GROUP BY o.id
ORDER BY o.id;

-- name: GetOrderTags
-- summary: The tags of one order.
-- output-model-type: class
SELECT o.id, o.tags
FROM orders AS o
WHERE o.id = @orderId;

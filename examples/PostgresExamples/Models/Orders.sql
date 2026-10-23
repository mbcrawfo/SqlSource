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
SELECT o.id, sum(l.quantity * l.unit_price) AS total
FROM orders AS o
JOIN order_lines AS l ON l.order_id = o.id
GROUP BY o.id
ORDER BY o.id;

-- name: CountShippedOrders
-- summary: How many orders of each user have shipped.
SELECT u.id, count(o.shipped_at) AS shipped
FROM users AS u
LEFT JOIN orders AS o ON o.user_id = u.id
GROUP BY u.id
ORDER BY u.id;

-- name: ListUserDisplayNames
-- summary: The name of each user with each word capitalized.
-- column: display_name not null
SELECT u.id, initcap(u.name) AS display_name
FROM users AS u
ORDER BY u.id;

-- name: GetOrderTags
-- summary: The tags of one order.
SELECT o.id, o.tags
FROM orders AS o
WHERE o.id = @orderId;

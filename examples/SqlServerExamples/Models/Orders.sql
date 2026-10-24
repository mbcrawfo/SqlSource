-- The queries of the orders.  Their output is models, set by the attribute of StoreQueries, so each gets a model of
-- its parameters and one of its rows, from its entry in Orders.sql.json.  The parameters are in camelCase, which Dapper
-- binds to the properties of the input model.

-- name: GetOrder
-- summary: One order and the name of its customer.
SELECT o.id, o.status, o.placed_at, o.shipped_on, u.name AS customer_name
FROM orders AS o
JOIN users AS u ON u.id = o.user_id
WHERE o.id = @orderId;

-- name: ListOrdersPlacedSince
-- summary: The latest orders placed since a time, newest first.
-- param: @count int
SELECT TOP (@count) id, placed_at
FROM orders
WHERE placed_at >= @since
ORDER BY placed_at DESC;

-- name: CountShippedOrders
-- summary: How many orders of each user have shipped.
SELECT u.id, COUNT(o.shipped_on) AS shipped
FROM users AS u
LEFT JOIN orders AS o ON o.user_id = u.id
GROUP BY u.id
ORDER BY u.id;

-- name: ListOrderMonths
-- summary: Each order with the last day of the month it was placed in.
-- column: month_end not null
SELECT id, EOMONTH(placed_at) AS month_end
FROM orders
ORDER BY id;

-- name: AddOrder
-- summary: Places a new order for a user, and returns what the database filled in.
INSERT INTO orders (user_id)
OUTPUT inserted.id, inserted.status, inserted.placed_at
VALUES (@userId);

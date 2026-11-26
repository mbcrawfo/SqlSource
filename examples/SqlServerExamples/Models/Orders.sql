-- The queries of the orders.  Their output is models, set by this folder's sqlsource.json, so each gets a model of
-- its parameters and one of its rows, from its entry in Orders.sql.json.  The parameters are in camelCase, which Dapper
-- binds to the properties of the input model.

-- name: GetOrder
-- summary: One order and the name of its customer.
SELECT
    o.id,
    o.status,
    o.placed_at,
    o.shipped_on,
    u.name AS customer_name
FROM orders AS o
INNER JOIN users AS u ON o.user_id = u.id
WHERE o.id = @orderId;

-- name: ListOrdersPlacedSince
-- summary: The latest orders placed since a time, newest first.
-- param: @count int
SELECT TOP (@count)
    id,
    placed_at
FROM orders
WHERE placed_at >= @since
ORDER BY placed_at DESC;

-- name: CountShippedOrders
-- summary: How many orders of each user have shipped.
SELECT
    u.id,
    COUNT(o.shipped_on) AS shipped
FROM users AS u
LEFT JOIN orders AS o ON u.id = o.user_id
GROUP BY u.id
ORDER BY u.id;

-- name: ListShippedOrders
-- summary: The orders that have shipped, with the day each one shipped.
SELECT
    id,
    shipped_on
FROM orders
WHERE shipped_on IS NOT NULL
ORDER BY shipped_on;

-- name: ListUsersAndOrders
-- summary: Every user and every order, matched by the order's user, with the user's id from either side.
SELECT
    u.name,
    o.id AS order_id,
    COALESCE(u.id, o.user_id) AS user_id
FROM users AS u
FULL JOIN orders AS o ON u.id = o.user_id
ORDER BY user_id, order_id;

-- name: ListOrderLineTotals
-- summary: The total of each line of each order, with the time the order was placed.
SELECT
    o.id,
    o.placed_at,
    l.line_number,
    l.line_total
FROM orders AS o
INNER JOIN order_lines AS l ON o.id = l.order_id
ORDER BY o.id, l.line_number;

-- name: ListOrderMonths
-- summary: Each order with the first day of the month it was placed in.
-- column: month_start not null
SELECT
    id,
    DATEFROMPARTS(YEAR(placed_at), MONTH(placed_at), 1) AS month_start
FROM orders
ORDER BY id;

-- name: ListShippingDays
-- summary: Each day from an order's placing to its shipping, numbered from 1.
WITH days AS (
    SELECT
        id AS order_id,
        CAST(placed_at AS date) AS calendar_day,
        1 AS day_number,
        shipped_on
    FROM orders
    WHERE shipped_on IS NOT NULL
    UNION ALL
    SELECT
        d.order_id,
        DATEADD(DAY, 1, d.calendar_day) AS calendar_day,
        d.day_number + 1 AS day_number,
        d.shipped_on
    FROM days AS d
    WHERE d.calendar_day < d.shipped_on
)

SELECT
    order_id,
    calendar_day,
    day_number
FROM days
ORDER BY order_id, day_number;

-- name: AddOrder
-- summary: Places a new order for a user, and returns what the database filled in.
INSERT INTO orders (user_id)
OUTPUT INSERTED.id, INSERTED.status, INSERTED.placed_at
VALUES (@userId);

-- name: MarkOrdersShipped
-- summary: Marks as shipped every pending order that has a day it shipped, and returns each order with that day.
UPDATE orders
SET status = 'shipped'
OUTPUT INSERTED.id, INSERTED.shipped_on
WHERE status = 'pending' AND shipped_on IS NOT NULL;

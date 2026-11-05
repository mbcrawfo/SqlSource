-- The queries of the orders, at the default output, codegen, whose markers put their methods on the class rather than
-- in an extension class.  ListOrdersOfUser, FindOrder and ListOrdersByStatuses are public static methods of
-- OrderQueries, which take the connection first, and CountOrderLines is a method of the class's private nested class
-- Sql, the default of a nested class, which only OrderQueries' own code calls.  ListOrdersByStatuses filters by a list:
-- @statuses, written after IN with no parentheses, is an IEnumerable<string>? of its input model, and a null list
-- leaves every order in.

-- name: ListOrdersOfUser -> many
-- summary: The orders of one user, newest first.
-- member-location: class
-- visibility: public
SELECT id, placed_at, shipped_at
FROM orders
WHERE user_id = @userId
ORDER BY placed_at DESC;

-- name: FindOrder -> one-optional
-- summary: One order, or null.
-- member-location: class
-- visibility: public
SELECT id, user_id, placed_at, shipped_at
FROM orders
WHERE id = @orderId;

-- name: CountOrderLines -> one
-- summary: The number of lines of one order.
-- member-location: nested-class
SELECT count(*) AS line_count
FROM order_lines
WHERE order_id = @orderId;

-- name: ListOrdersByStatuses -> many
-- summary: The orders in any of the given statuses, or every order when the list is null, newest first.
-- member-location: class
-- visibility: public
SELECT id, status, placed_at
FROM orders
WHERE (@statuses IS NULL OR status IN @statuses)
ORDER BY placed_at DESC;

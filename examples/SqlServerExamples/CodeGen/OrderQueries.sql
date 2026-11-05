-- The queries of the orders, at the default output, codegen, whose markers put their methods on the class rather than
-- in an extension class.  ListOrdersOfUser and FindOrder are public static methods of OrderQueries, which take the
-- connection first, and CountOrderLines is a method of the class's private nested class Sql, the default of a nested
-- class, which only OrderQueries' own code calls.  The generator parameter of the header applies to every query of the
-- file, and FindOrder adds another to it.
-- generator: async-method-suffix

-- name: ListOrdersOfUser -> many
-- summary: The orders of one user, newest first.
-- member-location: class
-- visibility: public
SELECT id, placed_at, shipped_on
FROM orders
WHERE user_id = @userId
ORDER BY placed_at DESC;

-- name: FindOrder -> one-optional
-- summary: One order, or null.
-- generator: sort-output
-- member-location: class
-- visibility: public
SELECT id, user_id, placed_at, shipped_on
FROM orders
WHERE id = @orderId;

-- name: CountOrderLines -> one
-- summary: The number of lines of one order.
-- member-location: nested-class
SELECT COUNT(*) AS line_count
FROM order_lines
WHERE order_id = @orderId;

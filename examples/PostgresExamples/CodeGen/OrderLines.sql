-- The queries of the order lines, at the default output, codegen.  Until the methods that run the queries arrive in a
-- later release, codegen gives what models gives: the SQL and the models, which OrderLineQueries reads through Dapper.
-- The result shape after the name says what that method will return; it is checked and has no effect yet.

-- name: ListOrderLines -> many
-- summary: The lines of one order, in order.
SELECT line_number, product, quantity, unit_price
FROM order_lines
WHERE order_id = @orderId
ORDER BY line_number;

-- The queries of the order lines, at the default output, codegen.  Each query gets its models and a method that runs
-- it, which OrderLineQueries calls.  The result shape after the name says what the method returns: many, every row,
-- as an array of the output model.  The type override gives each numeric of the file the type Price, whose converters
-- the method calls to read unit_price, and the project's column types give order_lines.order_id the type OrderId.
-- type-override: numeric=PostgresExamples.CodeGen.Price

-- name: ListOrderLines -> many
-- summary: The lines of one order, in order.
-- A parameter is no column of a table, so its marker gives it the type OrderId.  order_id comes last: the table's
-- columns in its own order would give the rows the table's model, OrderLinesDto.
-- param: @orderId -> OrderId
SELECT line_number, product, quantity, unit_price, order_id
FROM order_lines
WHERE order_id = @orderId
ORDER BY line_number;

-- name: ListLatestOrderPerProduct -> many
-- summary: The latest order that each product is on.
-- The largest order_id is computed, no column of a table, so the column types do not reach it, and the marker gives
-- it the type OrderId.
-- column: latest_order_id -> OrderId
SELECT product, max(order_id) AS latest_order_id
FROM order_lines
GROUP BY product
ORDER BY product;

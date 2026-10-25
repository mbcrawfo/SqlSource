-- The queries of the order lines, at the default output, codegen.  Each query gets its models and a method that runs
-- it, which OrderLineQueries calls.  The result shape after the name says what the method returns: many, every row,
-- as an array of the output model.  The type override gives each decimal of the file the type Price, whose converters
-- the method calls to read unit_price.
-- type-override: decimal=SqlServerExamples.CodeGen.Price

-- name: ListOrderLines -> many
-- summary: The lines of one order, in order.
SELECT line_number, product, quantity, unit_price
FROM order_lines
WHERE order_id = @orderId
ORDER BY line_number;

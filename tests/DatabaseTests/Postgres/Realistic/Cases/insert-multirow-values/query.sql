-- name: AddTwoOrderLines
INSERT INTO order_lines (order_id, line_no, product_id, quantity, unit_price)
VALUES
    (@order_id, 1, @first_product_id, @first_quantity,
        (SELECT p.price FROM products p WHERE p.id = @first_product_id)),
    (@order_id, 2, @second_product_id, @second_quantity,
        (SELECT p.price FROM products p WHERE p.id = @second_product_id))
RETURNING order_id, line_no, product_id, quantity, unit_price, quantity * unit_price AS line_total;

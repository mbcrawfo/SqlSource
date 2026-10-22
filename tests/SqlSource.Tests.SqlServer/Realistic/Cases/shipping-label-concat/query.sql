-- name: ListShippingLabels
-- param: @status varchar(10)
SELECT
    o.id AS order_id,
    CONCAT(COALESCE(o.ship_name, c.name), N', ', o.ship_city) AS label,
    o.ship_name + N', ' + o.ship_city AS address_line,
    CONCAT_WS(N' / ', c.phone, c.email) AS contact,
    N'Attn: ' + c.company AS attention
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.status = @status
ORDER BY o.placed_at, o.id;

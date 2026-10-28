-- name: SearchContactNames
-- param: @q nvarchar(100)
SELECT c.name, c.email, c.phone, 'customer' AS source
FROM dbo.customers AS c
WHERE c.name LIKE @q
UNION
SELECT o.ship_name, c.email, c.phone, 'recipient'
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.ship_name LIKE @q
ORDER BY name, source;

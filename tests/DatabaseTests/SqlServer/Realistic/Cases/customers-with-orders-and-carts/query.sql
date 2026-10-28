-- name: ListCustomersWithOrdersAndOpenCarts
SELECT c.id, c.name, c.email, c.time_zone
FROM dbo.customers AS c
JOIN dbo.orders AS o ON o.customer_id = c.id
INTERSECT
SELECT c.id, c.name, c.email, c.time_zone
FROM dbo.customers AS c
JOIN dbo.carts AS ct ON ct.customer_id = c.id
WHERE ct.checked_out_at IS NULL
ORDER BY name, id;

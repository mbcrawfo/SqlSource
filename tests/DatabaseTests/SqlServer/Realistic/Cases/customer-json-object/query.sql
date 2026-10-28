-- name: ListCustomerCards
-- param: @status varchar(10)
SELECT
    c.id,
    c.name,
    JSON_OBJECT('email': c.email, 'phone': c.phone, 'company': c.company) AS contact,
    JSON_OBJECT('email': c.email, 'phone': c.phone ABSENT ON NULL) AS reachable_by,
    JSON_ARRAY(c.email, c.phone) AS channels,
    (
        SELECT TOP (1) JSON_OBJECT('id': o.id, 'status': o.status, 'city': o.ship_city)
        FROM dbo.orders AS o
        WHERE o.customer_id = c.id
        ORDER BY o.placed_at DESC, o.id DESC
    ) AS last_order
FROM dbo.customers AS c
WHERE c.status = @status
ORDER BY c.name;

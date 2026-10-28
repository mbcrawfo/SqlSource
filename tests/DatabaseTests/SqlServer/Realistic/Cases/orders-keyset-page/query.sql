-- name: ListOrdersAfter
-- param: @after_placed datetime2(0)
-- param: @after_id int
-- param: @take int
SELECT TOP (@take)
    o.id,
    o.placed_at,
    o.status,
    c.name AS customer_name,
    o.ship_city,
    o.shipped_at
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.placed_at > @after_placed
    OR (o.placed_at = @after_placed AND o.id > @after_id)
ORDER BY o.placed_at, o.id;

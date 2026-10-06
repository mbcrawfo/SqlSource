-- SqlSource: keep-comments

-- name: GetOrder
SELECT id, total /* in cents */
FROM orders
WHERE id = @id;

-- name: OrdersOf
-- The schema is a token, so this query is a method.
SELECT o.id FROM {{schema}}.orders AS o INNER JOIN {{schema}}.users AS u ON u.id = o.user_id WHERE {{filter}};

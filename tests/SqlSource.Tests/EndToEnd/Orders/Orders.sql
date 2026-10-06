-- SqlSource: preserve-comments

-- name: GetOrder
SELECT id, total /* in cents */
FROM orders
WHERE id = @id;

-- name: OrdersOf
-- This query has a token, so it gets no constant.
SELECT id FROM {{schema}}.orders WHERE user_id = @userId;

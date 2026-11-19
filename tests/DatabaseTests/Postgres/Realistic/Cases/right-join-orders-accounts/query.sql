-- name: ListOrdersWithOpenAccount
SELECT
    coalesce(a.id, o.account_id) AS account_id,
    a.name,
    a.status,
    o.id AS order_id,
    o.placed_at
FROM accounts a
-- The RIGHT JOIN, whose left side can be null, is what this case describes.
RIGHT JOIN orders o ON o.account_id = a.id AND a.status <> 'closed'  -- noqa: CV08
ORDER BY o.placed_at;

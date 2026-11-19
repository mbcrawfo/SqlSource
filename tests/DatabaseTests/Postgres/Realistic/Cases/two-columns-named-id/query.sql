-- name: ListAccountsAndTheirOrderIds
SELECT
    a.id,
    -- Two columns named id are what this case describes.
    o.id,  -- noqa: AL08
    a.name,
    o.placed_at
FROM accounts a
LEFT JOIN orders o ON o.account_id = a.id
WHERE a.status = @status
ORDER BY a.id, o.id;

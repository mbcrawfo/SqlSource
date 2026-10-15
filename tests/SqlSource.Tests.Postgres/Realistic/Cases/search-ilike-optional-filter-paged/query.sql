-- name: SearchAccounts
-- param: @status account_status null
SELECT a.id, a.name, a.email, a.status, a.closed_at
FROM accounts a
WHERE (a.name ILIKE '%' || @q || '%' OR a.email ILIKE '%' || @q || '%')
    AND (@status::account_status IS NULL OR a.status = @status)
ORDER BY a.name
LIMIT @limit OFFSET @offset;

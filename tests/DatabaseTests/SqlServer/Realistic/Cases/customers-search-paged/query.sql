-- name: SearchCustomers
-- param: @status varchar(10) null
SELECT c.id, c.name, c.email, c.phone, c.status
FROM dbo.customers AS c
WHERE c.name LIKE @q
    AND (@status IS NULL OR c.status = @status)
ORDER BY c.name, c.id
OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;

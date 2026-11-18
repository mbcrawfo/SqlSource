-- dialect: mssql
-- name: Count
DECLARE @n int = 1;
SELECT @@ROWCOUNT, a@b, 'x@y.z', [p@q], @n /* @c */ FROM t WHERE id = @id; -- @d

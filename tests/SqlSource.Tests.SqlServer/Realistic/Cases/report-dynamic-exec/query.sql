-- name: ListOrdersSorted
-- param: @since datetime2(0)
-- param: @sort_column nvarchar(128)
DECLARE @sql nvarchar(max) = N'
SELECT o.id, o.placed_at, o.status, c.name AS customer_name, o.ship_city
FROM dbo.orders AS o
JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.placed_at >= @since
ORDER BY ' + QUOTENAME(@sort_column) + N', o.id;';

EXEC sys.sp_executesql @sql, N'@since datetime2(0)', @since = @since;

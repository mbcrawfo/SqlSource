-- dialect: postgres
-- name: Page
-- param: @page int
SELECT * FROM t ORDER BY id {{paging:LIMIT 20 OFFSET @page}};

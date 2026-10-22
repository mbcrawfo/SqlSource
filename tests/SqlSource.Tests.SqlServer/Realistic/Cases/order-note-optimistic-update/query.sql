-- name: UpdateOrderNote
-- param: @id int
-- param: @note nvarchar(200) null
-- param: @version binary(8)
UPDATE dbo.orders
SET note = @note
OUTPUT
    inserted.id,
    deleted.note AS previous_note,
    inserted.note,
    inserted.row_version
WHERE id = @id
    AND row_version = @version;

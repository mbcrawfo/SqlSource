-- name: UpdateOrderNote
-- param: @id int
-- param: @note nvarchar(200) null
-- param: @version binary(8)
UPDATE dbo.orders
SET note = @note
OUTPUT
    INSERTED.id,
    DELETED.note AS previous_note,
    INSERTED.note,
    INSERTED.row_version
WHERE
    id = @id
    AND row_version = @version;

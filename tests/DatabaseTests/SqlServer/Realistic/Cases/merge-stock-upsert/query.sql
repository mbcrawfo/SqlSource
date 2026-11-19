-- name: UpsertStock
-- param: @product_id int
-- param: @on_hand int
-- param: @reorder_at int null
MERGE dbo.stock WITH (HOLDLOCK) AS s
USING (VALUES (@product_id, @on_hand, @reorder_at)) AS src(product_id, on_hand, reorder_at)
    ON s.product_id = src.product_id
WHEN MATCHED
    THEN
    UPDATE SET s.on_hand = src.on_hand, s.reorder_at = src.reorder_at, s.updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED
    THEN
    INSERT (product_id, on_hand, reorder_at, updated_at)
    VALUES (src.product_id, src.on_hand, src.reorder_at, SYSUTCDATETIME())
OUTPUT
    $action AS change,
    INSERTED.product_id,
    INSERTED.on_hand,
    INSERTED.reorder_at,
    INSERTED.updated_at,
    DELETED.on_hand AS previous_on_hand,
    DELETED.reorder_at AS previous_reorder_at,
    DELETED.updated_at AS previous_updated_at;

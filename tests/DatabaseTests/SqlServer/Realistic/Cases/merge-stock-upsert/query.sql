-- name: UpsertStock
-- param: @product_id int
-- param: @on_hand int
-- param: @reorder_at int null
MERGE dbo.stock WITH (HOLDLOCK) AS s
USING (VALUES (@product_id, @on_hand, @reorder_at)) AS src (product_id, on_hand, reorder_at)
    ON s.product_id = src.product_id
WHEN MATCHED THEN
    UPDATE SET s.on_hand = src.on_hand, s.reorder_at = src.reorder_at, s.updated_at = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (product_id, on_hand, reorder_at, updated_at)
    VALUES (src.product_id, src.on_hand, src.reorder_at, SYSUTCDATETIME())
OUTPUT
    $action AS change,
    inserted.product_id,
    inserted.on_hand,
    inserted.reorder_at,
    inserted.updated_at,
    deleted.on_hand AS previous_on_hand,
    deleted.reorder_at AS previous_reorder_at,
    deleted.updated_at AS previous_updated_at;

-- name: SyncOrders
MERGE dbo.orders AS t
USING dbo.users AS s ON t.user_id = s.id
WHEN MATCHED THEN UPDATE SET total = @total
WHEN NOT MATCHED THEN INSERT (user_id, total) VALUES (s.id, 0)
WHEN NOT MATCHED BY SOURCE THEN DELETE
OUTPUT $action AS action, inserted.id AS inserted_id, inserted.total AS inserted_total,
    deleted.id AS deleted_id, deleted.total AS deleted_total;

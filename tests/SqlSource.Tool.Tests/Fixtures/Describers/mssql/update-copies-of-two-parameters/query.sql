-- name: RetireBetween
UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @since OR deleted_at < @since OR created_at > @since_ OR deleted_at > @since_;

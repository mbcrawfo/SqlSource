-- name: RetireBefore
UPDATE dbo.users SET deleted_at = SYSUTCDATETIME() WHERE created_at < @before OR deleted_at < @before;

-- name: PurgeUsers
-- param: @tenant int
DELETE FROM users WHERE status = @status {{filter:AND deleted_at IS NOT NULL}};

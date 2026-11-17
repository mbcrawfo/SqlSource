-- dialect: postgres
-- name: Smile
SELECT '😀' AS face, {{col:name}} FROM users WHERE id = @id;

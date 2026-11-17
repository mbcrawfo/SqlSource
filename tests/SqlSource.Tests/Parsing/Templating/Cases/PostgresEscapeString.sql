-- dialect: postgres
-- name: Escaped
SELECT E'it\'s -- {{no}}' AS a, 'it''s' AS b, {{col:name}} FROM users WHERE id = @id;

-- dialect: postgres
-- A token inside a quoted region is still a token.
-- name: Escaped
SELECT E'it\'s -- {{q}}' AS a, 'it''s' AS b, {{col:name}} FROM users WHERE id = @id;

-- dialect: postgres
-- A token inside a quoted region is still a token.
-- name: Dollar
SELECT $$ -- x $$ AS a, $tag$ {{q}} $tag$ AS b, {{col:name}} FROM users;

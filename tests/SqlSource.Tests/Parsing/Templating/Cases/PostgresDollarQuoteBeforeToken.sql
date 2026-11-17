-- dialect: postgres
-- name: Dollar
SELECT $$ -- x $$ AS a, $tag$ {{no}} $tag$ AS b, {{col:name}} FROM users;

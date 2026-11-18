-- dialect: mysql,ansi_quotes
-- name: Quoted
SELECT "col\", {{col:name}}, 'it\'s' FROM users;

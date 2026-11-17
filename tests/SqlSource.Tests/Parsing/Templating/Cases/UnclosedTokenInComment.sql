-- dialect: postgres
-- name: ActiveUsers
SELECT * FROM users -- TODO: add {{filter: ...
WHERE {{cond:active}} /* {{note: */ AND id = @id;

-- name: FindAccountsByIdOrName
SELECT a.id, a.name, a.email
FROM accounts a
WHERE a.name = @term OR a.id = @term
ORDER BY a.id;

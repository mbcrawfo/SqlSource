-- The query of the method locations on the type, claimed by InternalLocationQueries and by PrivateLocationQueries,
-- which share its models.

-- name: GetLocatedName -> one
-- summary: The name of an account, by its id.
SELECT id, name FROM accounts WHERE id = @accountId;

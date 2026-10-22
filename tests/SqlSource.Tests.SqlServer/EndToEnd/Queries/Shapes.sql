-- The queries of the shapes of models, claimed by ShapeQueries.
-- input-model-suffix: Filter
-- collection-type: IReadOnlyList

-- name: SearchAccounts -> many
-- summary: The accounts with a name like a pattern, or with an id.
-- generator: sort-input
SELECT id, name
FROM dbo.accounts
WHERE name LIKE @name OR id = @accountId
ORDER BY id;

-- name: ListAccountColumns
-- summary: Some columns of every account, in a class whose properties are sorted.
-- generator: sort-output
-- output-model-type: class
SELECT status, name, id
FROM dbo.accounts
ORDER BY id;

-- name: GetHeadline
-- summary: The headline of an account.
-- output-model: AccountHeadline
SELECT id, name AS headline
FROM dbo.accounts
WHERE id = @accountId;

-- name: LookUpAccount
-- summary: An account, by its email, with an input model of another namespace.
-- input-model: SqlSource.Tests.SqlServer.EndToEnd.Shared.AccountLookup
-- input-model-type: class
SELECT id, name, status
FROM dbo.accounts
WHERE email = @email;

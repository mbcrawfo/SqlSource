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

-- name: SearchAccountsUnpooled -> many
-- summary: SearchAccounts under no-array-pool, which reads its rows into a List, for the allocation tests: the query's
-- summary: own list of generator parameters replaces the attribute's, so a second claim could not give it one.
-- generator: sort-input no-array-pool
-- input-model: SearchAccountsFilter
-- output-model: SearchAccountsView
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
-- input-model: SqlSource.DatabaseTests.SqlServer.EndToEnd.Shared.AccountLookup
-- input-model-type: class
SELECT id, name, status
FROM dbo.accounts
WHERE email = @email;

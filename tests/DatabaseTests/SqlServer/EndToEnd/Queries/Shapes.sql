-- The queries of the shapes of models, claimed by ShapeQueries, here and in tests/DatabaseTests/QueryMethodAllocation,
-- which links this file: its header gives both claims their models' shapes, suffixes and namespace.
-- input-model-suffix: Filter
-- output-model-suffix: View
-- input-model-type: sealed-class
-- output-model-type: record
-- model-namespace: SqlSource.DatabaseTests.SqlServer.EndToEnd.Shapes
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
-- summary: own list of generator parameters replaces every other list, so only a copy of it can take another.
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

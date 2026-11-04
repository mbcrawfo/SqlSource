-- The queries of the shapes of models, claimed by ShapeQueries.  SearchAccounts has its collection type by its marker,
-- and the others by this header's.
-- input-model-suffix: Filter
-- output-model-suffix: View
-- input-model-type: sealed-class
-- output-model-type: record
-- model-namespace: SqlSource.DatabaseTests.Postgres.EndToEnd.Shapes
-- collection-type: List

-- name: SearchAccounts -> many
-- summary: The accounts with a name like a pattern, or with an id.
-- generator: sort-input
-- collection-type: IReadOnlyList
SELECT id, name
FROM accounts
WHERE name LIKE @name OR id = @accountId
ORDER BY id;

-- name: ListAccountColumns
-- summary: Some columns of every account, in a class whose properties are sorted.
-- generator: sort-output
-- output-model-type: class
SELECT status, name, id
FROM accounts
ORDER BY id;

-- name: GetHeadline
-- summary: The headline of an account.
-- output-model: AccountHeadline
SELECT id, name AS headline
FROM accounts
WHERE id = @accountId;

-- name: LookUpAccount
-- summary: An account, by its email, with an input model of another namespace.
-- input-model: SqlSource.DatabaseTests.Postgres.EndToEnd.Shared.AccountLookup
-- input-model-type: class
SELECT id, name, status
FROM accounts
WHERE email = @email;

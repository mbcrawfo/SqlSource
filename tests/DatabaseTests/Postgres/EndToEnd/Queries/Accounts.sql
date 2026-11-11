-- The queries of the accounts, claimed by AccountQueries and by Reporting.AccountReports, which each get their models.
-- A copy named with Unpooled or Sync after its original is the original under array-pool=off or method-type=sync, or
-- both, for the allocation tests of tests/DatabaseTests/QueryMethodAllocation, which link this file.

-- name: GetAccount
-- summary: One account, its columns in another order than the table's.
SELECT name, id, email
FROM accounts
WHERE id = @accountId;

-- name: ListAccounts
-- summary: Every account, by SELECT *.
SELECT *
FROM accounts
ORDER BY id;

-- name: ListAccountsUnpooled
-- summary: ListAccounts again, without the array pool.
-- generator: array-pool=off
SELECT *
FROM accounts
ORDER BY id;

-- name: ListAccountsSync
-- summary: ListAccounts, by a synchronous method.
-- generator: method-type=sync
SELECT *
FROM accounts
ORDER BY id;

-- name: ListAccountsUnpooledSync
-- summary: ListAccountsUnpooled, by a synchronous method.
-- generator: array-pool=off method-type=sync
SELECT *
FROM accounts
ORDER BY id;

-- name: ListAccountsInTableOrder
-- summary: Every account, by the whole list of columns in the table's order.
SELECT id, name, email, status, created_at, closed_at, name_length
FROM accounts
ORDER BY id;

-- name: ListAccountsUntabled
-- summary: Every account, in a model named after the query.
-- generator: table-models=off
SELECT *
FROM accounts
ORDER BY id;

-- name: GetAccountName
-- summary: The name of an account, by its id.
-- output-model: AccountName
SELECT id, name
FROM accounts
WHERE id = @accountId;

-- name: GetAccountNameUnpooled
-- summary: GetAccountName again, without the array pool.
-- output-model: AccountName
-- generator: array-pool=off
-- input-model: GetAccountNameArgs
SELECT id, name
FROM accounts
WHERE id = @accountId;

-- name: GetAccountNameSync
-- summary: GetAccountName, by a synchronous method.
-- output-model: AccountName
-- generator: method-type=sync
-- input-model: GetAccountNameArgs
SELECT id, name
FROM accounts
WHERE id = @accountId;

-- name: GetAccountNameUnpooledSync
-- summary: GetAccountNameUnpooled, by a synchronous method.
-- output-model: AccountName
-- generator: array-pool=off method-type=sync
-- input-model: GetAccountNameArgs
SELECT id, name
FROM accounts
WHERE id = @accountId;

-- name: FindAccountName
-- summary: The name of an account, by its email.
-- output-model: AccountName
SELECT id, name
FROM accounts
WHERE email = @email;

-- name: ListClosedBefore
-- summary: The accounts closed before a time, or every account when it is null.
-- param: @closedBefore timestamptz null
SELECT id, name, closed_at
FROM accounts
WHERE @closedBefore IS NULL OR closed_at < @closedBefore
ORDER BY id;

-- name: ListClosedBeforeUnpooled
-- summary: ListClosedBefore again, without the array pool.
-- param: @closedBefore timestamptz null
-- generator: array-pool=off
-- input-model: ListClosedBeforeArgs
-- output-model: ListClosedBeforeDto
SELECT id, name, closed_at
FROM accounts
WHERE @closedBefore IS NULL OR closed_at < @closedBefore
ORDER BY id;

-- name: ListClosedBeforeSync
-- summary: ListClosedBefore, by a synchronous method.
-- param: @closedBefore timestamptz null
-- generator: method-type=sync
-- input-model: ListClosedBeforeArgs
-- output-model: ListClosedBeforeDto
SELECT id, name, closed_at
FROM accounts
WHERE @closedBefore IS NULL OR closed_at < @closedBefore
ORDER BY id;

-- name: ListClosedBeforeUnpooledSync
-- summary: ListClosedBeforeUnpooled, by a synchronous method.
-- param: @closedBefore timestamptz null
-- generator: array-pool=off method-type=sync
-- input-model: ListClosedBeforeArgs
-- output-model: ListClosedBeforeDto
SELECT id, name, closed_at
FROM accounts
WHERE @closedBefore IS NULL OR closed_at < @closedBefore
ORDER BY id;

-- The queries of the accounts, claimed by AccountQueries and by Reporting.AccountReports, which each get their models.

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

-- name: ListAccountsInTableOrder
-- summary: Every account, by the whole list of columns in the table's order.
SELECT id, name, email, status, created_at, closed_at, name_length
FROM accounts
ORDER BY id;

-- name: ListAccountsUntabled
-- summary: Every account, in a model named after the query.
-- generator: no-table-models
SELECT *
FROM accounts
ORDER BY id;

-- name: GetAccountName
-- summary: The name of an account, by its id.
-- output-model: AccountName
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

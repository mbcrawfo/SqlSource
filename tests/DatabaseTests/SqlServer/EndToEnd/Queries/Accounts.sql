-- The queries of the accounts, claimed by AccountQueries and by Reporting.AccountReports, which each get their models.

-- name: GetAccount
-- summary: One account, its columns in another order than the table's.
SELECT name, id, email
FROM dbo.accounts
WHERE id = @accountId;

-- name: ListAccounts
-- summary: Every account, by SELECT *, in the model of the table, whose name leaves out the schema dbo.
SELECT *
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountsInTableOrder
-- summary: Every account, by the whole list of columns in the table's order.
SELECT id, name, email, status, opened_on, created_at, closed_at, name_length
FROM dbo.accounts
ORDER BY id;

-- name: ListAccountsUntabled
-- summary: Every account, in a model named after the query.
-- generator: no-table-models
SELECT *
FROM dbo.accounts
ORDER BY id;

-- name: GetAccountName
-- summary: The name of an account, by its id.
-- output-model: AccountName
SELECT id, name
FROM dbo.accounts
WHERE id = @accountId;

-- name: FindAccountName
-- summary: The name of an account, by its email, a parameter of the alias type dbo.Email.
-- output-model: AccountName
SELECT id, name
FROM dbo.accounts
WHERE email = @email;

-- name: ListClosedBefore
-- summary: The accounts closed before a time, or every account when it is null.
-- param: @closedBefore datetimeoffset null
SELECT id, name, closed_at
FROM dbo.accounts
WHERE @closedBefore IS NULL OR closed_at < @closedBefore
ORDER BY id;

-- name: ListOpenedWithin
-- summary: The accounts opened in the days before a date, and on it.
-- The count of DATEADD is an int on SQL Server 2017 and a bigint on 2025, so both parameters declare their types,
-- and the entry is the same on both.
-- param: @days int
-- param: @asOf date
SELECT id, name, opened_on
FROM dbo.accounts
WHERE opened_on >= DATEADD(day, -@days, @asOf) AND opened_on <= @asOf
ORDER BY id;

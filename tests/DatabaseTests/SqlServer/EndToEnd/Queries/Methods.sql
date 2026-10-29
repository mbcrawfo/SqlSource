-- The queries of the result shapes, a token, the counts that rowcount returns and the run-time arguments, claimed by
-- MethodQueries.  Every write runs in its tests in a transaction that is rolled back.

-- name: TouchAccount
-- summary: Writes an account's status over itself: no rows, so its shape is rowcount, the one account it updates.
UPDATE dbo.accounts
SET status = status
WHERE id = @accountId;

-- name: SetAccountStatus -> none
-- summary: Sets the status of an account, and returns nothing.
UPDATE dbo.accounts
SET status = @status
WHERE id = @accountId;

-- name: GetAccountInRange -> one
-- summary: The one account whose id is in a range, which a range of none or of two refuses.
SELECT id, name
FROM dbo.accounts
WHERE id BETWEEN @firstId AND @lastId
ORDER BY id;

-- name: FindAccountInRange -> one-optional
-- summary: The account whose id is in a range, or none, which a range of two refuses.
SELECT id, name
FROM dbo.accounts
WHERE id BETWEEN @firstId AND @lastId
ORDER BY id;

-- name: ListAccountsWhere
-- summary: The accounts that a filter keeps, by default those up to an id, which a filter of its own need not use.
-- param: @lastId int
SELECT id, name
FROM dbo.accounts
{{filter:WHERE id <= @lastId}}
ORDER BY id;

-- name: AddTwoNotes -> rowcount
-- summary: Adds two notes to an account, whose OUTPUT the count includes.
INSERT INTO dbo.notes (account_id, kind, body, written_at)
OUTPUT INSERTED.id
VALUES (@accountId, @kind, @body, @writtenAt), (@accountId, @kind, @body, @writtenAt);

-- name: CountReadAccounts -> rowcount
-- summary: A SELECT, whose count is -1.
SELECT id, name
FROM dbo.accounts;

-- name: TouchAccountsQuietly -> rowcount
-- summary: Writes the status of two accounts over itself under SET NOCOUNT ON, which gives no count.  With no
-- parameter it is sent as a batch, whose SET lasts for the session.
SET NOCOUNT ON;
UPDATE dbo.accounts
SET status = status
WHERE id <= 2;

-- name: TouchAccountQuietly -> rowcount
-- summary: Writes the status of an account over itself under SET NOCOUNT ON, which gives no count.  With a parameter
-- it runs in sp_executesql, whose SET ends with it.
SET NOCOUNT ON;
UPDATE dbo.accounts
SET status = status
WHERE id = @accountId;

-- name: TouchAndCountAccounts -> rowcount
-- summary: Writes the status of the accounts up to an id over itself, then reads one account: the update's count.
UPDATE dbo.accounts
SET status = status
WHERE id <= @lastId;
SELECT id, name
FROM dbo.accounts
WHERE id = @accountId;

-- name: TouchAndGetAccount -> one
-- summary: Writes the status of the accounts up to an id over itself, then reads one account: the first result set.
UPDATE dbo.accounts
SET status = status
WHERE id <= @lastId;
SELECT id, name
FROM dbo.accounts
WHERE id = @accountId;

-- name: GetFirstOfTwoAccounts -> one
-- summary: Reads one account, then another: the first result set, the first account.
SELECT id, name
FROM dbo.accounts
WHERE id = @firstId;
SELECT id, name
FROM dbo.accounts
WHERE id = @secondId;

-- name: ListThenRaise
-- summary: The accounts into an array, a mark on the session, and then an error that the method throws.
-- StreamThenRaise of Streaming.sql, read into the default collection type: its test pins that the error is thrown by a
-- method that reads its rows into a collection too.  SET CONTEXT_INFO keeps its value for the session, which the test
-- reads from the same connection: the mark is there when the batch ran as far as the THROW.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SET CONTEXT_INFO 0x03;
THROW 50000, 'after the rows', 1;

-- name: ListThenFailConversion
-- summary: The accounts into an array, and then a conversion that fails, an error that the method throws.
-- StreamThenFailConversion of Streaming.sql, read into the default collection type: its test pins that the failed
-- conversion is thrown, and rolls back a transaction with XACT_ABORT off, for a method that reads its rows into a
-- collection too.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SELECT CAST('x' AS int);

-- name: GetTwoThenFailConversion -> one
-- summary: Two accounts, which one refuses, and then a conversion that fails, an error that the refusal hides.
-- The method throws at the second row, and the reader's disposal reads the failed conversion, which with XACT_ABORT
-- off rolls back a transaction: its tests pin that the method throws the rollback with the refusal inside it.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id <= 2
ORDER BY id;
SELECT CAST('x' AS int);

-- name: GetTwoThenRaise -> one
-- summary: Two accounts, which one refuses, and then an error that the refusal hides.
-- GetTwoThenFailConversion with a THROW in the place of the failed conversion: with XACT_ABORT off the THROW leaves
-- a transaction open, and under XACT_ABORT ON rolls it back.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id <= 2
ORDER BY id;
THROW 50000, 'after the rows', 1;

-- name: ListNullAtThirdThenFailConversion
-- summary: Every account, with a NULL for the name of the third, which the marker says is never NULL, and then a
-- conversion that fails, an error that the NULL's exception hides.
-- StreamNullAtThirdThenFailConversion of Streaming.sql, read into the default collection type.
-- output-model: CollectedAccount
-- column: name not null
SELECT id, CASE WHEN id = 3 THEN NULL ELSE name END AS name
FROM dbo.accounts
ORDER BY id;
SELECT CAST('x' AS int);

-- name: ListFailingAtSecondThenFailConversion
-- summary: Every account with 10 divided by its id less 2, which fails at the second, and then a conversion that
-- fails.
-- The divide by zero is the database's error, which the method throws as it is, and the reader's disposal reads the
-- failed conversion after it.
SELECT id, 10 / (id - 2) AS quotient
FROM dbo.accounts
ORDER BY id;
SELECT CAST('x' AS int);

-- name: RunThenRaise -> none
-- summary: The accounts, a mark on the session, and then an error, which a method that reads no rows reports.
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SET CONTEXT_INFO 0x04;
THROW 50000, 'after the rows', 1;

-- name: GetThenRaise -> one
-- summary: The first account, a mark on the session, and then an error that the method throws.
-- ListThenRaise under one, which reads its row and then reads again to see that there is no second: its test pins that
-- the error is thrown by a method that reads one row too.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id = 1;
SET CONTEXT_INFO 0x05;
THROW 50000, 'after the rows', 1;

-- name: FindThenRaise -> one-optional
-- summary: The first account, a mark on the session, and then an error that the method throws.
-- ListThenRaise under one-optional, which reads its row and then reads again to see that there is no second: its test
-- pins that the error is thrown by a method that reads at most one row too.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id = 1;
SET CONTEXT_INFO 0x06;
THROW 50000, 'after the rows', 1;

-- name: FindNoneThenRaise -> one-optional
-- summary: No account, a mark on the session, and then an error that the method throws rather than return null.
-- FindThenRaise with no row: its test pins that the error is thrown by a method that finds no row too.
-- output-model: CollectedAccount
SELECT id, name
FROM dbo.accounts
WHERE id = 0;
SET CONTEXT_INFO 0x08;
THROW 50000, 'after the rows', 1;

-- name: CountThenRaise -> rowcount
-- summary: The accounts, a mark on the session, and then an error, which a method that reads no rows reports.
-- RunThenRaise under rowcount, which reads no rows either.
SELECT id, name
FROM dbo.accounts
ORDER BY id;
SET CONTEXT_INFO 0x07;
THROW 50000, 'after the rows', 1;

-- name: MergeAccountStatuses -> rowcount
-- summary: Sets the status of the accounts up to an id by MERGE, whose OUTPUT the count includes.
MERGE dbo.accounts AS a
USING (SELECT id FROM dbo.accounts WHERE id <= @lastId) AS s
ON a.id = s.id
WHEN MATCHED THEN UPDATE SET status = @status
OUTPUT $action AS change, INSERTED.id;

-- name: ListAccountIdsCancellingOnRead
-- summary: The ids of the accounts, each read as a CancelOnRead, whose converter cancels the token of its test, so
-- that the read of the second row sees it cancelled.
-- type-override: int = SqlSource.DatabaseTests.SqlServer.EndToEnd.Types.CancelOnRead
SELECT id
FROM dbo.accounts
ORDER BY id;

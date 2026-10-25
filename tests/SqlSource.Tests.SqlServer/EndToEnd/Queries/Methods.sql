-- The queries of the result shapes, a token and the counts that rowcount returns, claimed by MethodQueries.  Every
-- write runs in its tests in a transaction that is rolled back.

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

-- name: MergeAccountStatuses -> rowcount
-- summary: Sets the status of the accounts up to an id by MERGE, whose OUTPUT the count includes.
MERGE dbo.accounts AS a
USING (SELECT id FROM dbo.accounts WHERE id <= @lastId) AS s
ON a.id = s.id
WHEN MATCHED THEN UPDATE SET status = @status
OUTPUT $action AS change, INSERTED.id;

-- name: WaitFiveSeconds -> one
-- summary: Waits five seconds before its row, longer than a command timeout of one second.
WAITFOR DELAY '00:00:05';
SELECT 1 AS n;

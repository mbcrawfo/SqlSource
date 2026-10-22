-- The queries that write and return rows through OUTPUT, claimed by WriteQueries.  Each is described by the rows of its
-- OUTPUT, and its tests run it in a transaction that is rolled back.

-- name: AddNote
-- summary: Adds a note to an account, and returns its id and its kind.
INSERT INTO dbo.notes (account_id, kind, body, written_at)
OUTPUT INSERTED.id, INSERTED.kind
VALUES (@accountId, @kind, @body, @writtenAt);

-- name: RenameAccount
-- summary: Renames an account, and returns its name before and after.
UPDATE dbo.accounts
SET name = @name
OUTPUT DELETED.name AS old_name, INSERTED.name AS new_name
WHERE id = @accountId;

-- name: MergeAccountStatus
-- summary: Sets the status of an account by MERGE, and returns the action it took and the account's id.
MERGE dbo.accounts AS a
USING (SELECT id FROM dbo.accounts WHERE id = @accountId) AS s
ON a.id = s.id
WHEN MATCHED THEN UPDATE SET status = @status
OUTPUT $action AS change, INSERTED.id;

-- The queries of the collection methods, claimed by BatchQueries: the header gives each its collection method, an
-- overload that runs it once for each item of a collection, in batches of 1,000, in a transaction of its own unless the
-- caller gives one or one is ambient.  SQL Server finds no temporary table by the name of another, so each insert names
-- its table by the token notes, which is dbo.notes when it is described, and a test passes #notes, a temporary table
-- of its own connection without the identity of dbo.notes, so that the rows land there, a method that commits leaves
-- the seed as it was, and a rollback leaves the table.  Each insert writes the id it is given, so that a test can
-- repeat one, and reads its account from dbo.accounts, so that an item of an account that does not exist adds no row
-- and returns none.
-- generator: collection-method

-- name: InsertNote -> one
-- summary: Adds a note with the id it is given to the account that an id names, and returns it.
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.account_id, inserted.body
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- name: InsertNoteRowCount -> rowcount
-- summary: Adds a note as InsertNote does, and counts it.
-- input-model: NoteToAdd
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- name: InsertNoteQuietly -> none
-- summary: InsertNoteRowCount again, under none, which returns nothing.
-- input-model: NoteToAdd
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- name: InsertNoteRowCountWithoutCounts -> rowcount
-- summary: Adds a note as InsertNoteRowCount does, after SET NOCOUNT ON, so that no command has a count.
-- input-model: NoteToAdd
SET NOCOUNT ON;
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- name: InsertConvertedNote -> one
-- summary: Adds a note as InsertNote does, whose kind is the rest of a body that starts with int:, converted to int,
-- summary: so that a body of int:x fails the conversion, an error that ends the batch and rolls back its transaction.
-- The LIKE and SUBSTRING type @body as nvarchar(4000), and the insert as nvarchar(max), so the marker gives the type.
-- param: @body nvarchar(max) not null
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.account_id, inserted.body
SELECT
    @id,
    id,
    CASE WHEN @body LIKE N'int:%' THEN CONVERT(varchar(10), CAST(SUBSTRING(@body, 5, 10) AS int)) ELSE 'call' END,
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: FindNotes
-- summary: The notes of the account that an id names.
-- output-model: BatchNote
SELECT id, account_id, body FROM dbo.notes WHERE account_id = @accountId ORDER BY id;

-- name: FindNote -> one-optional
-- summary: The note that an id names, if there is one.
-- output-model: BatchNote
SELECT id, account_id, body FROM dbo.notes WHERE id = @id;

-- name: FindAccountThenOther
-- summary: The account that an id names, and then a second result set of two columns, which a collection method
-- summary: refuses, since it would take the place of the next item's.
SELECT id FROM dbo.accounts WHERE id = @id;
SELECT 2 AS other, 3 AS third;

-- name: FindAccountWhenPositive
-- summary: The account that an id names, when the id is positive, and no result set otherwise, which a collection
-- summary: method refuses, since the next item's would take its place.
IF @id > 0
    SELECT id FROM dbo.accounts WHERE id = @id;

-- name: FindAccountThenEmpty
-- summary: The account that an id names, and then an empty result set of the same one column, which a collection method
-- summary: reads as the next item's, and refuses only when the batch has a result set left after the last item's.
SELECT id FROM dbo.accounts WHERE id = @id;
SELECT id FROM dbo.accounts WHERE id = 0;

-- name: GetAccountThenEmpty -> one
-- summary: FindAccountThenEmpty again, under one, which throws for the empty result set it reads as the next item's.
SELECT id FROM dbo.accounts WHERE id = @id;
SELECT id FROM dbo.accounts WHERE id = 0;

-- name: FindAccountThenText
-- summary: The account that an id names, and then a result set of one column of text, which a collection method reads
-- summary: as the next item's, and fails to read as an id.
SELECT id FROM dbo.accounts WHERE id = @id;
SELECT N'other' AS other;

-- name: InsertNoteThenOther -> one
-- summary: Adds a note as InsertNote does, and then returns a result set of one column, which a collection method
-- summary: refuses as the next item's, after the first item's insert, which the rollback of its transaction undoes.
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.account_id, inserted.body
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;
SELECT 2 AS other;

-- name: InsertNoteSerializable -> one
-- summary: Adds a note as InsertNote does, in a transaction of its own at the level serializable, and returns its id
-- summary: and, as its kind, the isolation level of the transaction it ran in, as sys.dm_exec_sessions numbers it.
-- generator: transaction=serializable
-- input-model: NoteToAdd
-- output-model: IsolatedNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.kind
SELECT
    @id,
    id,
    (SELECT CONVERT(varchar(10), transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id = @@SPID),
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: InsertNoteReadUncommitted -> one
-- summary: InsertNoteSerializable again, under transaction=read-uncommitted.
-- generator: transaction=read-uncommitted
-- input-model: NoteToAdd
-- output-model: IsolatedNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.kind
SELECT
    @id,
    id,
    (SELECT CONVERT(varchar(10), transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id = @@SPID),
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: InsertNoteReadCommitted -> one
-- summary: InsertNoteSerializable again, under transaction=read-committed.
-- generator: transaction=read-committed
-- input-model: NoteToAdd
-- output-model: IsolatedNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.kind
SELECT
    @id,
    id,
    (SELECT CONVERT(varchar(10), transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id = @@SPID),
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: InsertNoteRepeatableRead -> one
-- summary: InsertNoteSerializable again, under transaction=repeatable-read.
-- generator: transaction=repeatable-read
-- input-model: NoteToAdd
-- output-model: IsolatedNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.kind
SELECT
    @id,
    id,
    (SELECT CONVERT(varchar(10), transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id = @@SPID),
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: InsertNoteSnapshot -> one
-- summary: InsertNoteSerializable again, under transaction=snapshot, which the schema of app allows.
-- generator: transaction=snapshot
-- input-model: NoteToAdd
-- output-model: IsolatedNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.kind
SELECT
    @id,
    id,
    (SELECT CONVERT(varchar(10), transaction_isolation_level) FROM sys.dm_exec_sessions WHERE session_id = @@SPID),
    @body,
    SYSDATETIME()
FROM dbo.accounts
WHERE id = @accountId;

-- name: InsertNoteNoTransaction -> one
-- summary: InsertNote again, under transaction=off, so that each item commits on its own.
-- generator: transaction=off
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.account_id, inserted.body
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- name: InsertNoteInRepository -> one
-- summary: InsertNote again, at the output repository, as an instance method of BatchQueries that runs on its
-- summary: borrowed connection and takes no transaction.
-- output: repository
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO {{notes:dbo.notes}} (id, account_id, kind, body, written_at)
OUTPUT inserted.id, inserted.account_id, inserted.body
SELECT @id, id, 'call', @body, SYSDATETIME() FROM dbo.accounts WHERE id = @accountId;

-- The queries of the collection methods, claimed by BatchQueries: the header gives each its collection method, an
-- overload that runs it once for each item of a collection, in batches of 1,000, in a transaction of its own unless the
-- caller gives one or one is ambient.  The inserts name notes, and a test whose method commits makes a temporary table
-- notes on its own connection, which PostgreSQL searches before public, so that the rows land there and the seed is
-- left as it was.  Each insert writes the id it is given, so that a test can repeat one, and reads its account from
-- accounts, so that an item of an account that does not exist adds no row and returns none.
-- generator: collection-method

-- name: InsertNote -> one
-- summary: Adds a note with the id it is given to the account that an id names, and returns it.
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE
SELECT @id, id, 'call', @body, now() FROM accounts WHERE id = @accountId
RETURNING id, account_id, body;

-- name: InsertNoteRowCount -> rowcount
-- summary: Adds a note as InsertNote does, and counts it.
-- input-model: NoteToAdd
INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE
SELECT @id, id, 'call', @body, now() FROM accounts WHERE id = @accountId;

-- name: FindNotes
-- summary: The notes of the account that an id names.
-- output-model: BatchNote
SELECT id, account_id, body FROM notes WHERE account_id = @accountId ORDER BY id;

-- name: FindNote -> one-optional
-- summary: The note that an id names, if there is one.
-- output-model: BatchNote
SELECT id, account_id, body FROM notes WHERE id = @id;

-- name: InsertNoteSerializable -> one
-- summary: Adds a note as InsertNote does, in a transaction of its own at the level serializable, and returns its id
-- summary: and the isolation level of the transaction it ran in.
-- generator: transaction=serializable
-- input-model: NoteToAdd
INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE
SELECT @id, id, 'call', @body, now() FROM accounts WHERE id = @accountId
RETURNING id, current_setting('transaction_isolation') AS isolation;

-- name: InsertNoteNoTransaction -> one
-- summary: InsertNote again, under transaction=off, so that each batch commits or rolls back on its own.
-- generator: transaction=off
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE
SELECT @id, id, 'call', @body, now() FROM accounts WHERE id = @accountId
RETURNING id, account_id, body;

-- name: InsertNoteInRepository -> one
-- summary: InsertNote again, at the output repository, as an instance method of BatchQueries that runs on its
-- summary: borrowed connection and takes no transaction.
-- output: repository
-- input-model: NoteToAdd
-- output-model: BatchNote
INSERT INTO notes (id, account_id, kind, body, written_at) OVERRIDING SYSTEM VALUE
SELECT @id, id, 'call', @body, now() FROM accounts WHERE id = @accountId
RETURNING id, account_id, body;

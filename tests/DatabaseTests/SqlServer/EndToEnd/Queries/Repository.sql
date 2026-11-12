-- The queries of the repositories of RepositoryQueries.cs, at the output repository by the header, over the project's
-- methods: each type that claims the file gets each query as an instance method that takes its connection from the
-- type's own source, a connection string, a factory or a borrowed connection, and an interface of them.
-- output: repository
--
-- A copy named with Sync after its original is the original under method-type=sync, which gives it a synchronous
-- method that takes its connection from the type's synchronous source, and GetNoteBoth is GetNote under both, which
-- gives it the two.

-- name: GetNote -> one-optional
-- summary: The note that an id names, or null.
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
WHERE id = @id;

-- name: GetNoteSync -> one-optional
-- summary: GetNote, by a synchronous method.
-- generator: method-type=sync
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
WHERE id = @id;

-- name: GetNoteBoth -> one-optional
-- summary: GetNote, by a synchronous method and an asynchronous one, which is named with Async after it.
-- generator: method-type=both
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
WHERE id = @id;

-- name: FindNoteOfAccountSync -> one-optional
-- summary: The note of an account, if it has one, and an error when it has two, as alice does.
-- generator: method-type=sync
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
WHERE account_id = @accountId
ORDER BY id;

-- name: ListNotes
-- summary: Every note, streamed, so that a loop that breaks leaves its connection to the method's disposal.
-- collection-type: IAsyncEnumerable
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
ORDER BY id;

-- name: ListNotesSync
-- summary: ListNotes, by a synchronous method, which has no stream, so it reads every note into a list.
-- generator: method-type=sync
-- collection-type: List
-- output-model: RepositoryNote
SELECT id, account_id, body, remind_at
FROM dbo.notes
ORDER BY id;

-- name: TouchNote -> rowcount
-- summary: Writes the body of the note that an id names as it is, which changes nothing the other tests read.
-- A repository method takes no transaction, so the write is one that leaves the row as it was.
UPDATE dbo.notes
SET body = body
WHERE id = @id;

-- name: TouchNoteSync -> rowcount
-- summary: TouchNote, by a synchronous method.
-- generator: method-type=sync
UPDATE dbo.notes
SET body = body
WHERE id = @id;
